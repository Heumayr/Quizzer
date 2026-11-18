using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Spielablauf;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// R4-1 (Durchsicht M37), Weg Einzelvorschlag: nach einer gebuchten Runde ist „Runde wiederholen“ gesperrt - sonst bucht die
    /// Wiederholung ein zweites Mal aus derselben Zelle.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private PlayerResultContext KarteVon(TestableCurrentQuestionViewModel vm, int spieler)
            => vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world!.Players[spieler].Id);

        /// <summary>Eine Reaktionsrunde, in der <paramref name="schneller"/> gewinnt - bis das Ergebnis am Beamer steht.</summary>
        private async Task SpieleReaktionAsync(TestableCurrentQuestionViewModel vm, int schneller, bool wiederholt = false)
        {
            if (!wiederholt)
                await vm.StartMinispielRundeAsync();

            await AbgebenAsync(schneller, new { falseStart = false, milliseconds = 200 });
            await AbgebenAsync(1 - schneller, new { falseStart = false, milliseconds = 400 });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis") && KarteVon(vm, schneller).Suggestion == ScoreSuggestion.Right),
                "Nach der Runde steht kein Ergebnis am Beamer: " + vm.Reaktion.Kopfzeile);
        }

        private async Task<(int Anna, int Bert)> GebuchtAsync()
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world!.Coordinate.Id) ?? [];
            int Punkte(int i) => zeilen.Single(r => r.PlayerId == world.Players[i].Id).FinalScore;
            return (Punkte(0), Punkte(1));
        }

        [TestMethod]
        public async Task ARepeatAfterABookedRoundIsRefused()
        {
            var vm = await OeffneReaktionAsync(runden: 1);
            var feld = world!.Coordinate.CurrentPoints;

            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);

            await SpieleReaktionAsync(vm, schneller: 1);
            Assert.IsTrue(vm.RepeatMinigameRoundCommand.CanExecute(null), "Eine noch nicht gebuchte Runde lässt sich nicht wiederholen.");
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Speichern scheiterte.");
            Assert.AreEqual((0, feld), await GebuchtAsync(), "Bert hat die Runde nicht gebucht - der Test misst nichts.");

            Assert.IsFalse(vm.RepeatMinigameRoundCommand.CanExecute(null), "Nach der Buchung ist „Runde wiederholen“ nicht gesperrt.");
            StringAssert.Contains(vm.RepeatMinigameRoundToolTip, "Erst die Wertung zurücknehmen");

            await vm.WiederholeMinispielRundeAsync();
            Assert.IsTrue(vm.Reaktion.Kopfzeile.StartsWith("Ergebnis"), "Die gebuchte Runde wurde wiederholt: " + vm.Reaktion.Kopfzeile);

            // Wäre sie es doch: Anna gewinnt die Wiederholung, und das Schließen bucht ihren Vorschlag dazu.
            if (!vm.Reaktion.Kopfzeile.StartsWith("Ergebnis"))
                await SpieleReaktionAsync(vm, schneller: 0, wiederholt: true);

            vm.SchliessenBucht = true;
            await vm.ClosedForTestAsync();
            Assert.AreEqual((0, feld), await GebuchtAsync(), "Aus einer Zelle ist zweimal gebucht.");

            // Der Weg aus dem Tooltip: zurücknehmen, Zelle neu öffnen - dort ist nichts gebucht, und Wiederholen geht wieder.
            await merk.NimmZurueckAsync(world.Game);
            var neu = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await neu.LoadForTestAsync();
            Assert.AreEqual((0, 0), await GebuchtAsync(), "Die Rücknahme hat nicht gegriffen.");
            Assert.IsTrue(neu.RepeatMinigameRoundCommand.CanExecute(null), "Nach der Rücknahme bleibt „Runde wiederholen“ gesperrt.");
        }
    }
}
