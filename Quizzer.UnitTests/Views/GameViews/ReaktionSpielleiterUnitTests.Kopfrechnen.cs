using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Kopfrechnen beim Spielleiter (M35 C5), auf demselben Pruefstand wie die Reaktion: jede Antwort treibt
    /// das Rennen am Beamer, der Server schliesst an der Frist, und die meisten richtigen bekommen die
    /// Feldpunkte als Vorschlag mit Grund (E1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private const int KopfrechnenStartwert = 4711;

        [TestMethod]
        public async Task MentalArithmeticRacesOnTheBeamerAndProposesTheFieldPointsToTheBest()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.MentalArithmetic, f => { f.DurationSeconds = 30; f.Variant = KopfrechnenAufgaben.Mittel; });
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;
            spiel.Zufall = (_, _) => KopfrechnenStartwert;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            int Loesung(int n) => KopfrechnenAufgaben.Aufgabe(KopfrechnenStartwert, n, KopfrechnenAufgaben.Mittel).Loesung;

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Kopfrechnen läuft");
            Assert.AreEqual(2, vm.Reaktion.Zeilen.Count(z => z.Zeit == "0 richtig"), "Am Beamer stehen nicht beide bei null.");

            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            await AbgebenAsync(0, new { taskNumber = 1, answer = Loesung(1) }, MinigameKind.MentalArithmetic);
            await AbgebenAsync(1, new { taskNumber = 1, answer = Loesung(1) + 1 }, MinigameKind.MentalArithmetic);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == anna.CalculatedDisplayName && z.Zeit == "1 von 1 richtig")),
                "Die richtige Antwort erscheint nicht am Beamer.");
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == bert.CalculatedDisplayName && z.Zeit == "0 von 1 richtig")),
                "Die falsche Antwort erscheint nicht am Beamer.");

            uhr.Vor(TimeSpan.FromSeconds(30));

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("die meisten richtigen")),
                "Das Ende erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(vm.Reaktion.Zeilen.Single(z => z.Name == anna.CalculatedDisplayName).Sieger, "Am Beamer ist der Sieger nicht markiert.");
            Assert.IsTrue(Gespielt("Ende"), "Zum Ende kam kein Ton.");

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(Feldpunkte(vm), Karte(anna).CurrentScoreManipulation, "Der Sieger bekommt nicht die Feldpunkte.");
            StringAssert.Contains(Karte(anna).SuggestionText, "1 von 1 richtig");
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion, "Wer nicht vorn ist, bekommt einen Vorschlag.");
            Assert.AreEqual(200, Karte(anna).Messwert, "1 richtige in 30 s sind 2,00 je Minute - nicht auf der Karte (M35 C9).");
            Assert.IsNull(Karte(bert).Messwert, "Null richtige sind ein Messwert.");
            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
