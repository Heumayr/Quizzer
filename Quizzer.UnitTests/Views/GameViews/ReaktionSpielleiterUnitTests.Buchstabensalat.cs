using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Buchstabensalat beim Spielleiter (M35 C5), auf demselben Pruefstand wie die Reaktion: am Beamer das Wirrwarr
    /// und wer welchen Begriff geloest hat; sind alle geloest, schliesst der Server, und jeder bekommt seinen Anteil
    /// der Feldpunkte als Vorschlag mit Grund (E1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        /// <summary>Der Pruefstand legt zwei Zeilen „Hinweis 1" und „Hinweis 2" an - das sind die Begriffe.</summary>
        [TestMethod]
        public async Task WordScrambleShowsTheScrambleAndProposesAShareToEverySolver()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.WordScramble, f => f.DurationSeconds = 60);
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Begriff 1 von 2");
            Assert.IsFalse(vm.Reaktion.Kopfzeile.Contains("Hinweis 1", StringComparison.OrdinalIgnoreCase), "Am Beamer steht der offene Begriff.");
            Assert.AreEqual(2, vm.Reaktion.Zeilen.Count(z => z.Zeit == "0 gelöst"), "Am Beamer stehen nicht beide bei null.");

            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            await AbgebenAsync(1, new { termNumber = 1, guess = "Hinweis" }, MinigameKind.WordScramble);
            await AbgebenAsync(0, new { termNumber = 1, guess = "hinweis 1" }, MinigameKind.WordScramble);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains($"{anna.CalculatedDisplayName} hat „Hinweis 1“ gelöst")),
                "Die Loesung erscheint nicht am Beamer: " + vm.Reaktion.Kopfzeile);
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Begriff 2 von 2");
            Assert.IsTrue(vm.Reaktion.Zeilen.Any(z => z.Name == anna.CalculatedDisplayName && z.Zeit == "1 gelöst"));
            Assert.IsTrue(Gespielt("Tick"), "Zur Loesung kam kein Ton.");

            uhr.Vor(TimeSpan.FromSeconds(2));
            await AbgebenAsync(1, new { termNumber = 2, guess = "  HINWEIS 2 " }, MinigameKind.WordScramble);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("2 von 2 Begriffen gelöst")),
                "Sind alle geloest, erreicht das Ende den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(Gespielt("Ende"), "Zum Ende kam kein Ton.");

            var anteil = BuchstabensalatEvaluator.Punkte(1, 2, Feldpunkte(vm));
            foreach (var p in new[] { anna, bert })
            {
                Assert.AreEqual(ScoreSuggestion.Correction, Karte(p).Suggestion, $"{p.Designation} bekommt keinen Vorschlag.");
                Assert.AreEqual(anteil, Karte(p).CurrentScoreManipulation, $"{p.Designation} bekommt nicht den Anteil.");
                StringAssert.Contains(Karte(p).SuggestionText, "1 gelöst von 2");
            }

            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
