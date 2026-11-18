using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P2-3 (Durchsicht M37): „Runde zurücksetzen“ an einer gewöhnlichen Frage - Schätzfrage, Multiple Choice, ohne Teamregel und
    /// ohne Teamspiel. Wie bei Teamregel und Finale (V22) steht danach vor der nächsten Abgabe nichts Altes mehr auf den Karten:
    /// keine Antwort, keine Tasten, kein Schätzvorschlag, keine Auflösung im Fragefenster.
    /// </summary>
    [TestClass]
    public class RundeZuruecksetzenGewoehnlichUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.RundeZuruecksetzenGewoehnlich;

        private LocalBuzzer.Service.Base.LayoutStateManager Hub => serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        private async Task ZuruecksetzenAsync(TestableCurrentQuestionViewModel vm, BuzzerControlsLayout layout)
        {
            var vorher = Hub.ResetCount;
            await ((AsyncRelayCommand)vm.BuzzerControlsViewModel!.ResetRoundCommand).ExecuteAsync(layout);
            Assert.IsTrue(Hub.ResetCount > vorher, "Die Runde wurde nicht zurückgesetzt.");
        }

        /// <summary>Was nach dem Zurücksetzen noch von der alten Runde auf den Karten und im Fragefenster steht - leer, wenn nichts.</summary>
        private static string Altes(TestableCurrentQuestionViewModel vm)
        {
            var altes = new List<string>();

            foreach (var karte in vm.PlayersResultViewModel!.PlayerResultContextList)
            {
                var wer = karte.Player.CalculatedDisplayName;

                if (karte.InputResult != null)
                    altes.Add($"{wer}: Antwort {karte.InputResult.Value}");
                if (karte.AppreciateGuess != null)
                    altes.Add($"{wer}: Schätzung gewertet");
                if (karte.SelectionResult != null)
                    altes.Add($"{wer}: Tasten {string.Join(",", karte.SelectionResult.SelectedKeys)}");
                if (karte.Suggestion != ScoreSuggestion.None || karte.CurrentScoreManipulation != 0)
                    altes.Add($"{wer}: {karte.SuggestionText}");
            }

            if (vm.AppreciateOutcome != null)
                altes.Add("Fragefenster: " + vm.AppreciateSummary);

            return string.Join(" | ", altes);
        }

        private static void KeinAltes(TestableCurrentQuestionViewModel vm)
        {
            var altes = Altes(vm);
            Assert.IsTrue(altes.Length == 0, "Nach dem Zurücksetzen steht noch Altes im Fragefenster: " + altes);
        }

        private async Task SchaetzeAsync(TestableCurrentQuestionViewModel vm, int telefon, string wert)
        {
            await phones[telefon].InvokeAsync("SubmitInput", new { value = wert });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, telefon).InputResult?.Value == wert), $"Die Schätzung von Platz {telefon} kam nicht auf die Karte.");
        }

        [TestMethod]
        public async Task AnEstimateForgetsGuessesAndTheSuggestion()
        {
            FrageEinrichten = f => ((AppreciateQestion)f).ExpectedValue = 3798;
            var vm = await OeffneAsync(CellSpecialRule.None, typ: QuestionType.Appreciate);
            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout, "Die Schätzfrage liegt nicht als Eingabe auf den Telefonen.");

            await SchaetzeAsync(vm, 0, "3800");
            await SchaetzeAsync(vm, 1, "100");
            await SchaetzeAsync(vm, 2, "5000");
            Assert.IsTrue(await WarteAsync(() => vm.AppreciateOutcome != null), "Nach der letzten Schätzung wird nicht ausgewertet.");
            Assert.AreEqual(ScoreSuggestion.Right, Karte(vm, 0).Suggestion, "Am nächsten dran bekommt keinen Vorschlag - der Test misst nichts.");

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.Input);
            KeinAltes(vm);

            // Verlauf: neu schätzt nur Platz 2 - die alte Schätzung von Platz 0 kommt nicht wieder.
            await SchaetzeAsync(vm, 1, "3790");
            Assert.IsNull(Karte(vm, 0).InputResult, "Platz 0 hat eine Schätzung, ohne neu abzugeben.");
        }

        [TestMethod]
        public async Task MultipleChoiceForgetsTheKeys()
        {
            var vm = await OeffneAsync(CellSpecialRule.None, typ: QuestionType.MultipleChoice);
            Assert.AreEqual(BuzzerControlsLayout.KeySelect, vm.CurrentBuzzerLayout, "Multiple Choice liegt nicht als Tastenwahl auf den Telefonen.");

            for (var telefon = 0; telefon < 3; telefon++)
            {
                await phones[telefon].InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" } });
                var platz = telefon;
                Assert.IsTrue(await WarteAsync(() => Karte(vm, platz).SelectionResult != null), $"Die Wahl von Platz {platz} kam nicht auf die Karte.");
            }

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.KeySelect);
            KeinAltes(vm);
        }
    }
}
