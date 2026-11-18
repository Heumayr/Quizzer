using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P11-3 (Durchsicht M37, P12), der WPF-Rest von P10-3: der Hub löst eine Runde auf und reicht die Abgaben an die Oberfläche;
    /// bevor die sie liest, setzt die Spielleitung die Runde zurück. Die Warteschlange des Oberflächen-Threads bildet
    /// <see cref="ZurueckgehaltenerUiInvoker"/> nach - Hub, Telefone und Zurücksetzen sind echt. Aus der alten Runde darf danach
    /// nichts in der neuen aufgelöst werden: keine leere Auswertung, keine alte.
    /// </summary>
    [TestClass]
    public class AufloesenNachZuruecksetzenUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.AufloesenNachZuruecksetzen;

        private readonly ZurueckgehaltenerUiInvoker ui = new();

        [TestInitialize]
        public void SetUpUi() => ViewCommonBase.UiInvokerOverride = () => ui;

        [TestCleanup]
        public void TearDownUi() => ViewCommonBase.UiInvokerOverride = () => new InlineUiInvoker();

        /// <summary>Hält die Oberfläche an, lässt <paramref name="letzteAbgabe"/> die Runde schließen und setzt zurück, bevor die Oberfläche liest.</summary>
        private async Task ZuruecksetzenVorDemLesenAsync(TestableCurrentQuestionViewModel vm, BuzzerControlsLayout layout, Func<Task> letzteAbgabe)
        {
            ui.Halte = true;
            await letzteAbgabe();
            Assert.IsTrue(await WarteAsync(() => ui.Wartend >= 2), $"Die Auflösung kam nicht bei der Oberfläche an ({ui.Wartend} wartend).");

            // Das Zurücksetzen läuft auf dem Oberflächen-Thread selbst - was es dort anstößt, wartet nicht hinter der Auflösung.
            ui.Halte = false;
            await ((AsyncRelayCommand)vm.BuzzerControlsViewModel!.ResetRoundCommand).ExecuteAsync(layout);
            await ui.GibFreiAsync();
        }

        [TestMethod]
        public async Task AnEstimateResolvedJustBeforeAResetIsNotEvaluatedInTheNewRound()
        {
            FrageEinrichten = f => ((AppreciateQestion)f).ExpectedValue = 3798;
            var vm = await OeffneAsync(CellSpecialRule.None, typ: QuestionType.Appreciate);

            await phones[0].InvokeAsync("SubmitInput", new { value = "3800" });
            await phones[1].InvokeAsync("SubmitInput", new { value = "100" });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, 1).InputResult != null), "Die zweite Schätzung kam nicht auf die Karte.");

            await ZuruecksetzenVorDemLesenAsync(vm, BuzzerControlsLayout.Input, () => phones[2].InvokeAsync("SubmitInput", new { value = "5000" }));

            var tipps = vm.AppreciateOutcome?.Guesses.Count;
            Console.WriteLine($"P11-3 Schätzung: Auswertung {(tipps is null ? "keine" : tipps + " Tipps")}, "
                + $"Karten {string.Join(", ", Enumerable.Range(0, 3).Select(i => Karte(vm, i).InputResult?.Value ?? "-"))}");

            Assert.IsNull(vm.AppreciateOutcome, $"Die zurückgesetzte Runde wurde in der neuen ausgewertet ({tipps} Tipps).");
        }

        [TestMethod]
        public async Task KeysResolvedJustBeforeAResetAreNotResolvedInTheNewRound()
        {
            var vm = await OeffneAsync(CellSpecialRule.None, typ: QuestionType.MultipleChoice);

            for (var telefon = 0; telefon < 2; telefon++)
            {
                await phones[telefon].InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" } });
                var platz = telefon;
                Assert.IsTrue(await WarteAsync(() => Karte(vm, platz).SelectionResult != null), $"Die Wahl von Platz {platz} kam nicht auf die Karte.");
            }

            await ZuruecksetzenVorDemLesenAsync(vm, BuzzerControlsLayout.KeySelect,
                () => phones[2].InvokeAsync("SelectionResults", new { selectedKeys = new[] { "B" } }));

            var aufgeloest = vm.PlayersResultViewModel!.SelectionResultsDic;
            Console.WriteLine($"P11-3 Tasten: Auflösung {(aufgeloest is null ? "keine" : aufgeloest.Count + " Einträge")}, "
                + $"Karten {string.Join(", ", Enumerable.Range(0, 3).Select(i => Karte(vm, i).SelectionResult is { } s ? string.Join("", s.SelectedKeys) : "-"))}");

            Assert.IsNull(aufgeloest, $"Die zurückgesetzte Runde wurde in der neuen aufgelöst ({aufgeloest?.Count} Einträge).");
        }
    }
}
