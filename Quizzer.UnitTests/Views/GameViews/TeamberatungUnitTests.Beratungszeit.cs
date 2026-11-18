using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Beratungszeit (R5-5): sie läuft ab dem ersten Inhaltsschritt - auf dem Startbildschirm steht am Beamer nur die
    /// Fragenart - und endet früh, wenn alle Sprecher abgegeben haben.
    /// </summary>
    public partial class TeamberatungUnitTests
    {
        /// <summary>Teamberatung mit Beratungszeit, Spielfenster samt Countdown; die Frage ist offen, der Beamer zeigt noch nichts.</summary>
        private async Task<TestableCurrentQuestionViewModel> OeffneMitBeratungszeitAsync(int sekunden)
        {
            beratungszeit = sekunden;
            FensterEinrichten = vm =>
            {
                vm.GameMasterViewModel = new GameMasterViewModel { Game = world!.Game };
                return Task.CompletedTask;
            };

            return await OeffneTeamberatungAsync();
        }

        /// <summary>Zwei Schritte weiter: erst der Startbildschirm (nur die Fragenart), dann der Fragebildschirm.</summary>
        private static async Task ZurFrageAsync(TestableCurrentQuestionViewModel vm)
        {
            await vm.NextStepCommnad!.ExecuteAsync(null);
            Assert.IsTrue(vm.CurrentStep?.IsStart == true, "Der erste Schritt ist nicht der Startbildschirm - dann misst der Test nichts.");
            Assert.IsFalse(vm.Countdown!.Laeuft, "Die Beratungszeit läuft, während der Beamer nur die Fragenart zeigt.");

            await vm.NextStepCommnad.ExecuteAsync(null);
        }

        [TestMethod]
        public async Task TheCouncilTimeStartsWhenTheQuestionIsReadable()
        {
            var vm = await OeffneMitBeratungszeitAsync(5);
            Assert.IsFalse(vm.Countdown!.Laeuft, "Die Beratungszeit läuft ab dem Öffnen.");

            await ZurFrageAsync(vm);

            Assert.IsTrue(vm.Countdown.Laeuft, "Die Beratungszeit läuft nicht, obwohl die Frage am Beamer steht.");
            Assert.IsFalse(vm.CurrentStep!.IsStart);
        }

        [TestMethod]
        public async Task TheCouncilTimeEndsWhenAllSpeakersHaveAnswered()
        {
            var vm = await OeffneMitBeratungszeitAsync(30);
            await ZurFrageAsync(vm);
            Assert.IsTrue(vm.Countdown!.Laeuft, "Die Beratungszeit läuft nicht - dann misst der Test nichts.");

            await phones[0].InvokeAsync("SubmitInput", new { value = "Paris" });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, 0).InputResult?.Value == "Paris"), "Die Antwort von Rot kam nicht an.");
            Assert.IsTrue(vm.Countdown.Laeuft, "Die Beratungszeit endet, bevor alle Sprecher abgegeben haben.");

            await phones[2].InvokeAsync("SubmitInput", new { value = "Rom" });

            Assert.IsTrue(await WarteAsync(() => !vm.Countdown.Laeuft), "Alle Sprecher haben abgegeben, die Beratungszeit läuft weiter.");
            Assert.IsFalse(Hub.ZeitAbgelaufen, "Das frühe Ende sperrt wegen der Zeit.");
        }
    }
}
