using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// „Abschließen" im Fragefenster stempelt die Zelle (M37 E06) - im Speicher und in der Datenbank, über denselben
    /// Speicherweg wie <c>IsDone</c>. Ein zweites „Abschließen" (holt den Abschluss zurück auf den Beamer) lässt den Stempel.
    /// </summary>
    [TestClass]
    public class ZeitpunktDerZelleUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        [TestMethod]
        public async Task CompletingTheCellStampsItAndTheDatabaseKeepsTheStamp()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            var vorher = DateTimeOffset.Now;

            await TestEnvironment.RunCommandAsync(vm.SaveIsDoneFinishStateCommand);
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            var gestempelt = world.Coordinate.PlayedAt;
            Assert.IsNotNull(gestempelt, "Abschließen hat die Zelle nicht gestempelt.");
            Assert.IsTrue(gestempelt >= vorher && gestempelt <= DateTimeOffset.Now, $"Der Stempel {gestempelt} ist nicht jetzt.");

            using (var ctrl = new GameGridCoordinatesController())
                Assert.AreEqual(gestempelt, (await ctrl.GetAsync(world.Coordinate.Id))!.PlayedAt, "Der Stempel kam nicht in der Datenbank an.");

            await TestEnvironment.RunCommandAsync(vm.SaveIsDoneFinishStateCommand);
            Assert.AreEqual(gestempelt, world.Coordinate.PlayedAt, "Ein zweites Abschließen hat den Zeitpunkt verschoben.");
        }
    }
}
