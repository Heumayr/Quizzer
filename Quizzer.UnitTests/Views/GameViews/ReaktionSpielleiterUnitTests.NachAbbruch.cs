using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P8-2 (Durchsicht M37, P10): nach einem Abbruch - auch einem berechtigten mitten im Spiel - lehnt der Server „Runde starten“
    /// (Phase zu Ende) und „Runde wiederholen“ (abgebrochen) ab. Die Knöpfe folgen seinem Zustand und sagen am grauen Knopf, warum.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        [TestMethod]
        public async Task AfterAnAbortStartAndRepeatAreGreyAndSayWhy()
        {
            var vm = await OeffneReaktionAsync(runden: 2);
            var zustand = serverVm!.BuzzerControlsViewModel!.MinigameState!;

            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { falseStart = false, milliseconds = 200 });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = 400 });
            Assert.IsTrue(await WarteAsync(() => zustand.Phase == MinigamePhase.RoundOver), "Die erste Runde schließt nicht.");

            await vm.StartMinispielRundeAsync();
            Assert.AreEqual((MinigamePhase.Running, 2), (zustand.Phase, zustand.Round), "Die zweite Runde läuft nicht - der Test misst nichts.");

            var gemeldet = new List<string>();
            vm.PropertyChanged += (_, e) => { lock (gemeldet) gemeldet.Add(e.PropertyName ?? string.Empty); };

            await vm.BrecheMinispielAbAsync();
            Assert.IsTrue(await WarteAsync(() => zustand.Aborted), "Der Abbruch kam am Server nicht an - der Test misst nichts.");

            Assert.IsFalse(vm.StartMinigameRoundCommand.CanExecute(null), "Nach dem Abbruch ist „Runde starten“ frei, der Server lehnt ab.");
            Assert.IsFalse(vm.RepeatMinigameRoundCommand.CanExecute(null), "Nach dem Abbruch ist „Runde wiederholen“ frei, der Server lehnt ab.");
            StringAssert.Contains(vm.StartMinigameRoundToolTip, "abgebrochen", "„Runde starten“ sagt nicht, warum es gesperrt ist.");
            StringAssert.Contains(vm.RepeatMinigameRoundToolTip, "abgebrochen", "„Runde wiederholen“ sagt nicht, warum es gesperrt ist.");
            StringAssert.Contains(vm.RepeatMinigameRoundToolTip, "„Runde zurücksetzen“", "Die Erklärung nennt den Weg zurück nicht.");

            lock (gemeldet)
            {
                CollectionAssert.Contains(gemeldet, nameof(vm.StartMinigameRoundToolTip), "Der Abbruch meldet die Erklärung nicht neu.");
                CollectionAssert.Contains(gemeldet, nameof(vm.RepeatMinigameRoundToolTip), "Der Abbruch meldet die Erklärung nicht neu.");
            }

            // Am Befehl vorbei bleibt der Server beim Abbruch - was gesperrt ist, hätte ohnehin nichts getan.
            await vm.StartMinispielRundeAsync();
            await vm.WiederholeMinispielRundeAsync();
            Assert.AreEqual((MinigamePhase.Finished, true), (zustand.Phase, zustand.Aborted), "Der Server nahm nach dem Abbruch doch eine Runde an.");

            // Der Weg zurück: „Runde zurücksetzen“ bleibt frei und stellt das Minispiel auf Anfang.
            var zuruecksetzen = (AsyncRelayCommand)vm.BuzzerControlsViewModel!.ResetRoundCommand;
            Assert.IsTrue(zuruecksetzen.CanExecute(vm.CurrentBuzzerLayout), "Nach dem Abbruch ist „Runde zurücksetzen“ gesperrt - kein Weg zurück.");
            await zuruecksetzen.ExecuteAsync(vm.CurrentBuzzerLayout);

            Assert.IsTrue(vm.StartMinigameRoundCommand.CanExecute(null), "Nach dem Zurücksetzen bleibt „Runde starten“ gesperrt.");
            await vm.StartMinispielRundeAsync();
            Assert.AreEqual((MinigamePhase.Running, 1), (zustand.Phase, zustand.Round), "Nach dem Zurücksetzen startet keine neue Runde.");
            Assert.IsTrue(vm.RepeatMinigameRoundCommand.CanExecute(null), "In der neu gestarteten Runde ist „Runde wiederholen“ gesperrt.");
        }
    }
}
