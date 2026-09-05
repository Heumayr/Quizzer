using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Eine Standardfrage von vorne bis hinten - so, wie der Spielleiter sie spielt:
    /// oeffnen, Schritt fuer Schritt aufdecken, Aufloesung, schliessen.
    /// Ohne Fenster, ohne Buzzer, aber ueber dieselben ViewModels.
    /// </summary>
    [TestClass]
    public class DefaultQuestionPlayThroughUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static TestableCurrentQuestionViewModel OpenOn(TestGameBuilder world)
            => new() { Coordinate = world.Coordinate };

        [TestMethod]
        public async Task OpeningAQuestion_LoadsItsStepsAndPlayers()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 2);
            var vm = OpenOn(world);

            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.IsNotNull(vm.Coordinate?.QuestionBase);
            Assert.AreEqual(3, vm.Coordinate.QuestionBase.OrderedSteps.Length,
                "Zwei Hinweise und die Aufloesung - kein erfundener Startschritt mehr.");
            Assert.IsNotNull(vm.PlayersResultViewModel);
            Assert.AreEqual(2, vm.PlayersResultViewModel.Results.Count,
                "Fuer jeden Mitspieler ein Ergebnis.");
        }

        [TestMethod]
        public async Task SteppingForward_ReachesTheFinishStep()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 2);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();

            await vm.StartStepCommnad!.ExecuteAsync(null);
            var visited = new List<string?> { vm.CurrentStep?.Designation };

            while (vm.NextStep != null)
            {
                await vm.NextStepCommnad!.ExecuteAsync(null);
                visited.Add(vm.CurrentStep?.Designation);
            }

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            CollectionAssert.AreEqual(
                new[] { "Hinweis 1", "Hinweis 2", "Aufloesung" },
                visited.ToArray(),
                "Die Frage beginnt mit dem ersten echten Hinweis, nicht mit einem leeren Bild.");
            Assert.IsTrue(vm.CurrentStep?.IsFinish);
        }

        [TestMethod]
        public async Task SteppingForward_AsksBeforeRevealingTheAnswer()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();
            vm.Coordinate!.QuestionBase!.WarnOnResultStep = true;
            vm.Coordinate.QuestionBase.WarnOnFinishStep = true;

            await vm.StartStepCommnad!.ExecuteAsync(null);
            while (vm.NextStep != null)
                await vm.NextStepCommnad!.ExecuteAsync(null);

            CollectionAssert.Contains(prompt.Confirmations, "Lösungsschritt voraus");
        }

        [TestMethod]
        public async Task SteppingForward_WhenTheGameMasterSaysNo_StaysOnTheCurrentStep()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: false);

            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();
            vm.Coordinate!.QuestionBase!.WarnOnResultStep = true;

            await vm.StartStepCommnad!.ExecuteAsync(null);
            await vm.NextStepCommnad!.ExecuteAsync(null);
            var before = vm.CurrentStep;

            await vm.NextStepCommnad!.ExecuteAsync(null);

            Assert.AreSame(before, vm.CurrentStep,
                "Abgelehnt heisst stehenbleiben, nicht trotzdem aufdecken.");
        }

        [TestMethod]
        public async Task SteppingBack_ReturnsToThePreviousStep()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 2);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();

            await vm.StartStepCommnad!.ExecuteAsync(null);
            await vm.NextStepCommnad!.ExecuteAsync(null);
            Assert.AreEqual("Hinweis 2", vm.CurrentStep?.Designation);

            await vm.BackStepCommnad!.ExecuteAsync(null);

            Assert.AreEqual("Hinweis 1", vm.CurrentStep?.Designation);
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        [TestMethod]
        public async Task AQuestionWithoutHints_ShowsOnlyItsAnswer()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 0);
            var vm = OpenOn(world);

            await vm.LoadForTestAsync();

            Assert.IsNotNull(vm.Coordinate?.QuestionBase);
            Assert.AreEqual(1, vm.Coordinate.QuestionBase.OrderedSteps.Length,
                "Nur die Aufloesung - der QuestionValidator warnt beim Anlegen davor.");
        }

        [TestMethod]
        public async Task PlayingAMultipleChoiceQuestion_ShufflesButKeepsEveryOption()
        {
            await using var world = await TestGameBuilder.CreateAsync(
                questionType: QuestionType.MultipleChoice, normalStepCount: 4);
            var vm = OpenOn(world);

            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            var options = vm.Coordinate!.QuestionBase!.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation)
                .OrderBy(d => d)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "Hinweis 1", "Hinweis 2", "Hinweis 3", "Hinweis 4" }, options);
            Assert.AreEqual(BuzzerControlsLayout.KeySelect,
                vm.Coordinate.QuestionBase.BuzzerControlsLayout);
        }
    }
}
