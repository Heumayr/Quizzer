using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Bluff beim Spielleiter (M35 C6), auf demselben Pruefstand wie die Reaktion, mit drei Telefonen: Phase 1 zeigt am
    /// Beamer nur, wer abgegeben hat; dazwischen streicht der Spielleiter; nach der Wahl die Aufloesung mit Urhebern und
    /// die Vorschlaege (E1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        /// <summary>Der Pruefstand legt „Hinweis 1" und „Hinweis 2" an - die echte Antwort und eine gleichwertige Schreibweise.</summary>
        [TestMethod]
        public async Task BluffRunsTwoPhasesWithTheMasterStrikingInBetween()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.Bluff, f => f.DurationSeconds = 60, spielerzahl: 3);
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var carl = world.Players[2];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            bool BeamerNennt(string text) => vm.Reaktion.Kopfzeile.Contains(text) || vm.Reaktion.Zeilen.Any(z => z.Name.Contains(text) || z.Zeit.Contains(text));
            int Nummer(string text) => ((BluffClientInfo)((MinigameClientInfo)spiel.InfoFor(null)).KindInfo!).Options.Single(o => o.Text == text).Number;

            Assert.AreEqual(2, spiel.Setup.TotalRounds, "Der Bluff spielt nicht zwei Phasen.");

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Phase 1");
            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            await AbgebenAsync(0, new { bluff = "Paris" }, MinigameKind.Bluff);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Count(z => z.Zeit.Contains("abgegeben")) == 1), "Die Abgabe erscheint nicht am Beamer.");
            Assert.IsFalse(BeamerNennt("Paris"), "Am Beamer steht ein Bluff.");

            await AbgebenAsync(1, new { bluff = "hinweis 2" }, MinigameKind.Bluff);
            await AbgebenAsync(2, new { bluff = "Mailand" }, MinigameKind.Bluff);
            Assert.IsTrue(await WarteAsync(() => vm.BluffStreichenVisibility == Visibility.Visible), "Nach Phase 1 fehlt die Streichliste.");

            Assert.AreEqual(3, vm.BluffEintraege.Count);
            var gleich = vm.BluffEintraege.Single(e => e.Beschriftung.StartsWith("hinweis 2"));
            Assert.IsTrue(gleich.Gestrichen && !gleich.Aenderbar, "Ein Bluff gleich einer Schreibweise der echten ist nicht von selbst gestrichen.");
            StringAssert.Contains(vm.BluffEchteAntwort, "Hinweis 1");
            vm.BluffEintraege.Single(e => e.Beschriftung == "Mailand").Gestrichen = true;

            await vm.StartMinispielRundeAsync();
            Assert.AreEqual(Visibility.Collapsed, vm.BluffStreichenVisibility, "Waehrend der Wahl steht die Streichliste noch da.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Phase 2");
            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            // Von aussen gesehen, nicht von Carl: sein eigener (gestrichener) Bluff fehlte in seiner Liste ohnehin.
            CollectionAssert.AreEquivalent(new[] { "Hinweis 1", "Paris" },
                ((BluffClientInfo)((MinigameClientInfo)spiel.InfoFor(null)).KindInfo!).Options.Select(o => o.Text).ToArray(), "Gestrichene Bluffs stehen zur Wahl.");

            await AbgebenAsync(1, new { choice = Nummer("Paris") }, MinigameKind.Bluff);
            await AbgebenAsync(2, new { choice = Nummer("Paris") }, MinigameKind.Bluff);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Count(z => z.Zeit.Contains("gewählt")) == 2), "Die Wahl erscheint nicht am Beamer.");
            Assert.IsFalse(BeamerNennt("Hinweis 1"), "Waehrend der Wahl verraet der Beamer die echte Antwort.");

            await AbgebenAsync(0, new { choice = Nummer("Hinweis 1") }, MinigameKind.Bluff);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("echt war „Hinweis 1“")), "Das Ende erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(vm.Reaktion.Aufloesung.Any(z => z.Contains("Paris") && z.Contains("hereingefallen")), "Die Aufloesung nennt den Reinfall nicht.");

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(BluffEvaluator.PunkteEcht(Feldpunkte(vm)) + 2 * BluffEvaluator.PunkteReinfall(Feldpunkte(vm)), Karte(anna).CurrentScoreManipulation, "Echte gefunden und zwei Reinfaelle bringen nicht das Doppelte.");
            Assert.AreNotEqual(ScoreSuggestion.Correction, Karte(bert).Suggestion, "Ein gestrichener Bluff ohne Treffer bringt einen Vorschlag.");
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
