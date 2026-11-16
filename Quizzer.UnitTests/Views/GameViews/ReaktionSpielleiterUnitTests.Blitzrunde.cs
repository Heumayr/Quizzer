using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Blitzrunde beim Spielleiter (M35 C5), auf demselben Pruefstand wie die Reaktion: waehrend der Runde zeigt
    /// der Beamer nur den Fortschritt, haben alle alles beurteilt, schliesst der Server, und jeder bekommt seinen
    /// Anteil der Feldpunkte als Vorschlag - auch ein Minus (E1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        /// <summary>Der Pruefstand legt zwei Zeilen „Hinweis 1" und „Hinweis 2" an, beide ohne Haken - also falsch.</summary>
        [TestMethod]
        public async Task LightningRoundShowsProgressOnlyAndProposesTheNetShareToEveryone()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.LightningRound, f => f.DurationSeconds = 30);
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);

            await vm.StartMinispielRundeAsync();
            Assert.AreEqual(2, spiel.Setup.Content?.Count, "Die Zeilen der Frage erreichen den Server nicht.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Blitzrunde läuft");
            Assert.AreEqual(2, vm.Reaktion.Zeilen.Count(z => z.Zeit == "0 von 2 beantwortet"), "Am Beamer stehen nicht beide bei null.");

            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            await AbgebenAsync(0, new { statementNumber = 1, answer = false }, MinigameKind.LightningRound);
            await AbgebenAsync(1, new { statementNumber = 1, answer = true }, MinigameKind.LightningRound);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Count(z => z.Zeit == "1 von 2 beantwortet") == 2),
                "Der Fortschritt erscheint nicht am Beamer.");
            Assert.IsFalse(vm.Reaktion.Zeilen.Any(z => z.Zeit.Contains("richtig") || z.Sieger),
                "Waehrend der Runde verraet der Beamer, wer richtig lag.");
            Assert.AreEqual(0, vm.Reaktion.Aufloesung.Count, "Waehrend der Runde steht die Aufloesung am Beamer.");

            uhr.Vor(TimeSpan.FromMilliseconds(400));
            await AbgebenAsync(0, new { statementNumber = 2, answer = false }, MinigameKind.LightningRound);
            await AbgebenAsync(1, new { statementNumber = 2, answer = true }, MinigameKind.LightningRound);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("richtig +1")),
                "Haben alle alles beurteilt, erreicht das Ende den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(vm.Reaktion.Zeilen.Single(z => z.Name == anna.CalculatedDisplayName).Sieger, "Am Beamer ist der Beste nicht markiert.");
            Assert.IsTrue(await WarteAsync(() => Gespielt("Ende")), "Zum Ende kam kein Ton.");
            CollectionAssert.AreEqual(new[] { "✗ falsch – Hinweis 1", "✗ falsch – Hinweis 2" }, vm.Reaktion.Aufloesung.ToArray(),
                "Nach der Runde steht die Aufloesung nicht am Beamer.");

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(Feldpunkte(vm), Karte(anna).CurrentScoreManipulation, "Alle richtig bringt nicht die Feldpunkte.");
            StringAssert.Contains(Karte(anna).SuggestionText, "2 richtig, 0 falsch");
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(bert).Suggestion, "Alle falsch bringt keinen Vorschlag.");
            Assert.AreEqual(-Feldpunkte(vm), Karte(bert).CurrentScoreManipulation, "Alle falsch bringt nicht das Minus der Feldpunkte.");
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
