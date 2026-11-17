using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Erklaerbaer beim Spielleiter (M35 C6), auf demselben Pruefstand wie die Reaktion: den Begriff sieht nur das
    /// Spielleiterfenster, nie der Beamer; der Erklaerer kann nicht raten; der erste Treffer schliesst die Runde, Rater und
    /// Erklaerer bekommen den Topf als Vorschlag (E1). „Verraten" nimmt ihn wieder weg - nach der Runde wie mittendrin.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        /// <summary>Der Pruefstand legt zwei Zeilen „Hinweis 1" und „Hinweis 2" an - das sind die Begriffe.</summary>
        [TestMethod]
        public async Task ExplainItShowsTheTermOnlyToTheMasterAndBetrayedRoundsCountForNobody()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.ExplainIt, f => { f.Rounds = 2; f.DurationSeconds = 60; }, ausloeser: 0);
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            bool BeamerNennt(string text) => vm.Reaktion.Kopfzeile.Contains(text) || vm.Reaktion.Zeilen.Any(z => z.Name.Contains(text) || z.Zeit.Contains(text));

            Assert.AreEqual(Visibility.Visible, vm.MinispielPaarVisibility, "Die Wahl des Erklaerers fehlt im Spielleiterfenster.");
            Assert.AreEqual(Visibility.Collapsed, vm.PaarPartnerVisibility, "Der Erklaerbaer fragt nach einem Gegenueber.");
            Assert.AreEqual("Spieler festlegen", vm.PaarKnopfBeschriftung, "Der Knopf spricht beim Erklaerbaer von einem Paar.");

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, $"{anna.CalculatedDisplayName} erklärt");
            StringAssert.Contains(vm.ErklaerbaerBegriff, "Hinweis 1", "Der Spielleiter sieht den Begriff nicht.");
            Assert.AreEqual(Visibility.Visible, vm.ErklaerbaerVisibility);
            Assert.IsFalse(BeamerNennt("Hinweis"), "Am Beamer steht der Begriff.");

            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            await AbgebenAsync(0, new { guess = "Hinweis 1" }, MinigameKind.ExplainIt);
            await Task.Delay(200);
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Der Versuch der Erklaererin hat die Runde geschlossen.");

            await AbgebenAsync(1, new { guess = "hinweis 1" }, MinigameKind.ExplainIt);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("hat „Hinweis 1“ erraten")),
                "Der Treffer erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(await WarteAsync(() => Gespielt("Ende")), "Zum Ende der Runde kam kein Ton.");

            var topf = ErklaerbaerEvaluator.Topf(Feldpunkte(vm), 2);
            foreach (var p in new[] { anna, bert })
            {
                Assert.AreEqual(ScoreSuggestion.Correction, Karte(p).Suggestion, $"{p.Designation} bekommt keinen Vorschlag.");
                Assert.AreEqual(topf, Karte(p).CurrentScoreManipulation, $"{p.Designation} bekommt nicht den Topf.");
            }

            // Nachtraeglich verraten: die Runde zaehlt nicht mehr, fuer beide.
            await ((AsyncRelayCommand)vm.VerratenCommand).ExecuteAsync(null);
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "verraten");
            Assert.AreEqual(ScoreSuggestion.None, Karte(anna).Suggestion, "Die Erklaererin behaelt den Topf einer verratenen Runde.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion, "Der Rater behaelt den Topf einer verratenen Runde.");

            // Mittendrin verraten: die Runde endet sofort und bringt niemandem etwas.
            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.ErklaerbaerBegriff, "Hinweis 2");
            await ((AsyncRelayCommand)vm.VerratenCommand).ExecuteAsync(null);

            Assert.IsTrue(await WarteAsync(() => spiel.Phase == MinigamePhase.Finished && vm.Reaktion.Kopfzeile.Contains("„Hinweis 2“ – verraten")),
                "Verraten mitten in der Runde schliesst sie nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion);
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
