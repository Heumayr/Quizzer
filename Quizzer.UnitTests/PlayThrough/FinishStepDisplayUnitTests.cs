using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Was am Ende einer Zelle auf dem Beamer steht.
    /// <para>
    /// <c>CalculateOrderdSteps</c> erfindet einen Abschlussschritt, wenn keiner hinterlegt ist -
    /// einen leeren. Wurde er beim Abschliessen auf den Spielerbildschirm gestellt, sahen die
    /// Mitspieler bis zum Schliessen des Fensters nichts mehr. Der zuletzt gezeigte Schritt
    /// bleibt jetzt stehen.
    /// </para>
    /// </summary>
    [TestClass]
    public class FinishStepDisplayUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 2, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>
        /// Fuehrt einen Async-Befehl aus und wartet ihn ab. Der Umweg ueber den Helfer haelt
        /// die Nullpruefung an einer Stelle - ein direkter Cast erzeugte CS8600/CS8602.
        /// </summary>
        private static Task RunAsync(System.Windows.Input.ICommand? command)
        {
            if (command is not AsyncRelayCommand asyncCommand)
                throw new AssertFailedException("Der erwartete Async-Befehl fehlt.");

            return asyncCommand.ExecuteAsync(null);
        }

        /// <summary>Entfernt den Abschlussschritt, den der Builder anlegt.</summary>
        private async Task RemoveFinishStepAsync()
        {
            using var ctrl = new QuestionStepResourcesController();

            var schritte = await ctrl.GetAllAsync();

            foreach (var schritt in schritte.Where(s => s.QuestionBaseId == world.Question.Id && s.IsFinish))
            {
                await ctrl.DeleteAsync(schritt.Id);
            }

            await ctrl.SaveChangesAsync();
        }

        /// <summary>
        /// Ohne hinterlegten Abschlussschritt bleibt der zuletzt gezeigte stehen.
        /// </summary>
        [TestMethod]
        public async Task WithoutAFinishStepTheLastOneStaysOnScreen()
        {
            await RemoveFinishStepAsync();

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            // Einen Schritt aufdecken, damit etwas auf dem Beamer steht.
            await RunAsync(vm.NextStepCommnad);

            var gezeigt = vm.CurrentStep;

            Assert.IsNotNull(gezeigt, "Nach dem ersten Weiterschalten muss ein Schritt laufen.");

            await RunAsync(vm.SaveIsDoneFinishStateCommand);

            Assert.IsNotNull(vm.CurrentStep,
                "Der Spielerbildschirm ist leer - die Mitspieler saehen bis zum Schliessen nichts.");

            Assert.AreEqual(gezeigt!.Id, vm.CurrentStep!.Id,
                "Es haette der zuletzt gezeigte Schritt stehenbleiben muessen.");
        }

        /// <summary>
        /// Die Gegenrichtung: mit einem echten Abschlussschritt wird auf diesen gewechselt.
        /// Ohne diese Probe waere ein pauschales "nie wechseln" nicht aufgefallen.
        /// </summary>
        [TestMethod]
        public async Task WithARealFinishStepItIsShown()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            await RunAsync(vm.NextStepCommnad);

            var abschluss = vm.FinishStep;

            Assert.IsNotNull(abschluss, "Der Builder legt einen Abschlussschritt an.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(abschluss!.StepText),
                "Der Abschlussschritt des Builders traegt Text - sonst misst dieser Test nichts.");

            await RunAsync(vm.SaveIsDoneFinishStateCommand);

            Assert.AreEqual(abschluss.Id, vm.CurrentStep?.Id,
                "Ein echter Abschlussschritt muss auf den Beamer kommen.");
        }
    }
}
