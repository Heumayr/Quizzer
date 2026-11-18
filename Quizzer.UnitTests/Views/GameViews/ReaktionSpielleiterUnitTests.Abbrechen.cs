using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P6-3 (Durchsicht M37, P8): „Minispiel abbrechen“ nach dem Ende. Gemessen vor der Sperre: der Abbruch änderte weder
    /// Buchung noch Vorschläge - nur den Beamer („abgebrochen“ statt „Ergebnis“), das Telefon („beendet“ statt „vorbei“), den
    /// Ton und ein zweites Bewertungsfenster; dafür nahm er „Runde wiederholen“ am Server still weg. Deshalb gesperrt.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        [TestMethod]
        public async Task AbortingIsRefusedOnceTheMinigameIsOver()
        {
            var vm = await OeffneReaktionAsync(runden: 1);
            var zustand = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            Assert.IsTrue(vm.AbortMinigameCommand.CanExecute(null), "Vor dem Start lässt sich das Minispiel nicht abbrechen.");
            var gemeldet = 0;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.AbortMinigameToolTip)) gemeldet++; };

            await SpieleReaktionAsync(vm, schneller: 1);
            Assert.AreEqual(MinigamePhase.Finished, zustand.Phase, "Das Minispiel ist nicht zu Ende - der Test misst nichts.");
            var fenster = vm.ShowResultWindowCalls;

            Assert.IsFalse(vm.AbortMinigameCommand.CanExecute(null), "Nach dem Ende ist „Minispiel abbrechen“ nicht gesperrt.");
            StringAssert.Contains(vm.AbortMinigameToolTip, "schon zu Ende", "Die Erklärung sagt nicht, warum der Abbruch gesperrt ist.");
            Assert.IsTrue(gemeldet > 0, "Das Ende meldet die Erklärung nicht neu - der Knopf zeigt die alte.");
            gemeldet = 0;

            // Auch am Befehl vorbei: nichts erreicht den Server, nichts ändert die Anzeige.
            await vm.BrecheMinispielAbAsync();
            await Task.Delay(300);
            Assert.AreEqual(0, prompt.Confirms.Count, "Der gesperrte Abbruch fragt trotzdem nach.");
            Assert.IsFalse(zustand.Aborted, "Der Abbruch nach dem Ende erreichte den Server.");
            StringAssert.StartsWith(vm.Reaktion.Kopfzeile, "Ergebnis", "Der Beamer zeigt nach dem Ende einen Abbruch.");
            Assert.AreEqual(fenster, vm.ShowResultWindowCalls, "Der Abbruch nach dem Ende öffnet das Bewertungsfenster erneut.");

            // Der Weg, den die Spielleitung nach dem Ende braucht (E1), bleibt: wiederholen - und darin wieder abbrechen.
            Assert.IsTrue(vm.RepeatMinigameRoundCommand.CanExecute(null), "Nach dem Ende lässt sich die Runde nicht wiederholen.");
            await vm.WiederholeMinispielRundeAsync();
            Assert.AreEqual(MinigamePhase.Running, zustand.Phase, "Die Wiederholung läuft nicht.");
            Assert.IsTrue(vm.AbortMinigameCommand.CanExecute(null), "In der wiederholten Runde lässt sich nicht abbrechen.");
            StringAssert.StartsWith(vm.AbortMinigameToolTip, "Beendet das Minispiel sofort", "Die Erklärung folgt der Wiederholung nicht.");
            Assert.IsTrue(gemeldet > 0, "Die Wiederholung meldet die Erklärung nicht neu - der Knopf zeigt die alte.");
        }

        /// <summary>„Runde zurücksetzen“ nach dem Ende stellt das Minispiel auf Anfang - dort lässt es sich wieder abbrechen.</summary>
        [TestMethod]
        public async Task AResetAfterTheEndAllowsAbortingAgain()
        {
            var vm = await OeffneReaktionAsync(runden: 1);
            await SpieleReaktionAsync(vm, schneller: 1);
            Assert.IsFalse(vm.AbortMinigameCommand.CanExecute(null), "Nach dem Ende ist „Minispiel abbrechen“ nicht gesperrt - der Test misst nichts.");

            var gemeldet = 0;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.AbortMinigameToolTip)) gemeldet++; };
            await serverVm!.BuzzerControlsViewModel!.ResetRoundAsync(BuzzerControlsLayout.Minigame);

            Assert.IsTrue(vm.AbortMinigameCommand.CanExecute(null), "Nach dem Zurücksetzen lässt sich nicht abbrechen.");
            Assert.IsTrue(gemeldet > 0, "Das Zurücksetzen meldet die Erklärung nicht neu - der Knopf zeigt die alte.");
        }
    }
}
