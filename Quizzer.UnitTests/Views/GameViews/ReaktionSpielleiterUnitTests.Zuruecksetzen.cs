using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.UnitTests.PlayThrough;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P3-1 (aus P3): „Runde zurücksetzen“ nach einer gebuchten Runde stellt das Minispiel am Server auf Anfang - „Runde starten“
    /// spielte es dann neu, und das Schließen buchte aus derselben Zelle ein zweites Mal (dieselbe Form wie R4-1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        [TestMethod]
        public async Task AStartAfterResettingABookedRoundIsRefused()
        {
            var vm = await OeffneReaktionAsync(runden: 1);
            var feld = world!.Coordinate.CurrentPoints;

            await SpieleReaktionAsync(vm, schneller: 1);
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Speichern scheiterte.");
            Assert.AreEqual((0, feld), await GebuchtAsync(), "Bert hat die Runde nicht gebucht - der Test misst nichts.");

            await ((AsyncRelayCommand)vm.BuzzerControlsViewModel!.ResetRoundCommand).ExecuteAsync(vm.CurrentBuzzerLayout);
            Assert.IsFalse(vm.StartMinigameRoundCommand.CanExecute(null), "Nach Buchung und Zurücksetzen ist „Runde starten“ nicht gesperrt.");
            StringAssert.Contains(vm.StartMinigameRoundToolTip, "Erst die Wertung zurücknehmen");

            // Der Weg, der zweimal buchte: starten, Anna gewinnt, das Schließen bucht ihren Vorschlag dazu.
            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { falseStart = false, milliseconds = 200 });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = 400 });
            await WarteAsync(() => KarteVon(vm, 0).Suggestion == ScoreSuggestion.Right);

            vm.SchliessenBucht = true;
            await vm.ClosedForTestAsync();
            Assert.AreEqual((0, feld), await GebuchtAsync(), "Aus einer Zelle ist zweimal gebucht.");
        }
    }
}
