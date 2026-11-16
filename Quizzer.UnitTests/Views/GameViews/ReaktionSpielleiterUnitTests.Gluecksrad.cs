using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Das Gluecksrad beim Spielleiter (M35 C4): „Runde starten" dreht, der Beamer zeigt das Rad mit Zeiger,
    /// der Dreher waehlt sein Ziel am Telefon oder das Rad landet an der Serveruhr - und die Karten bekommen
    /// die Bewegungen mit Grund (E1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private async Task<(TestableCurrentQuestionViewModel Vm, StellbareUhr Uhr)> DreheRadAsync(WheelSegment feld)
        {
            var vm = await OeffneMinispielAsync(MinigameKind.WheelOfFortune, f => f.Share = 50, ausloeser: 0);
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;
            spiel.Zufall = (_, _) => GluecksradEvaluator.Felder.ToList().IndexOf(feld);

            StringAssert.Contains(vm.PaarWaehlerBeschriftung, "Wer dreht", "Die Paarwahl sagt nicht, wer dreht.");

            await vm.StartMinispielRundeAsync();
            Assert.AreEqual("Das Glücksrad dreht sich …", vm.Reaktion.Kopfzeile);
            Assert.AreEqual(1, vm.Reaktion.Zeilen.Count(z => z.Name.StartsWith(GluecksradTafel.Zeiger)),
                "Am Beamer steht nicht genau ein Feld unter dem Zeiger.");

            return (vm, uhr);
        }

        [TestMethod]
        public async Task TheSpinnerChoosesOnThePhoneAndBothCardsGetTheMove()
        {
            var (vm, uhr) = await DreheRadAsync(WheelSegment.Steal);
            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            var anteil = Punkteanteil.Von(Feldpunkte(vm), 50);

            uhr.Vor(TimeSpan.FromSeconds(3));
            await AbgebenAsync(0, new { chosen = bert.Id }, MinigameKind.WheelOfFortune);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains($"{anna.CalculatedDisplayName} beklaut {bert.CalculatedDisplayName}")),
                "Die Aufloesung erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(anteil, Karte(anna).CurrentScoreManipulation, "Der Dreher bekommt nicht den Anteil.");
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(bert).Suggestion);
            Assert.AreEqual(-anteil, Karte(bert).CurrentScoreManipulation, "Das Ziel verliert nicht den Anteil.");
            StringAssert.Contains(Karte(bert).SuggestionText, "Glücksrad (Klauen)");
            Assert.IsTrue(await WarteAsync(() => Gespielt("Ende")), "Zum Ergebnis kam kein Ton.");
        }

        [TestMethod]
        public async Task WithoutTargetTheWheelLandsAtTheServerClock()
        {
            var (vm, uhr) = await DreheRadAsync(WheelSegment.Double);
            var anna = world!.Players[0];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            var anteil = Punkteanteil.Von(Feldpunkte(vm), 50);

            await Task.Delay(3 * 50);
            Assert.AreEqual("Das Glücksrad dreht sich …", vm.Reaktion.Kopfzeile, "Das Rad steht vor dem Ende des Countdowns.");

            uhr.Vor(TimeSpan.FromSeconds(3));

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Glücksrad: Verdoppeln")),
                "Das Rad landet nicht an der Serveruhr: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(2 * anteil, karten.Single(k => k.Player.Id == anna.Id).CurrentScoreManipulation,
                "Verdoppeln bringt nicht den doppelten Anteil.");
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
