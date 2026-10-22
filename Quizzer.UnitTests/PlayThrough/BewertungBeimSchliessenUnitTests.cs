using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// 2026-10-22: eine gesetzte, nicht gespeicherte Bewertung geht beim Schließen des
    /// Ergebnisfensters nicht mehr verloren.
    /// </summary>
    [TestClass]
    public class BewertungBeimSchliessenUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static async Task<int> PunkteInDerDatenbankAsync(TestGameBuilder world, Guid spieler)
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? new();
            return zeilen.Single(r => r.PlayerId == spieler).FinalScore;
        }

        [TestMethod]
        public async Task ClosingTheResultWindowWritesWhatWasScored()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            var ergebnisse = vm.PlayersResultViewModel!;
            var karte = ergebnisse.PlayerResultContextList[0];

            karte.RightSuggestionCommand.Execute(null);
            Assert.IsTrue(ergebnisse.HatOffeneBewertungen);
            Assert.AreEqual(0, await PunkteInDerDatenbankAsync(world, karte.Player.Id), "Vor dem Schliessen steht schon etwas da - dann misst der Test nichts.");

            await ergebnisse.ClosedForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.IsTrue(await PunkteInDerDatenbankAsync(world, karte.Player.Id) > 0, "Die Bewertung ging beim Schliessen verloren.");
            Assert.IsFalse(ergebnisse.HatOffeneBewertungen);
        }

        /// <summary>Was schon gespeichert ist, wird beim Schließen nicht ein zweites Mal aufaddiert.</summary>
        [TestMethod]
        public async Task ClosingAfterSavingDoesNotCountTwice()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            var ergebnisse = vm.PlayersResultViewModel!;
            var karte = ergebnisse.PlayerResultContextList[0];

            karte.RightSuggestionCommand.Execute(null);
            Assert.IsTrue(await ergebnisse.TrySaveAsync());
            var gespeichert = await PunkteInDerDatenbankAsync(world, karte.Player.Id);

            await ergebnisse.ClosedForTestAsync();

            Assert.AreEqual(gespeichert, await PunkteInDerDatenbankAsync(world, karte.Player.Id));
        }

        [TestMethod]
        public async Task AnOwnValueOfZeroIsNothingToSave()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            var karte = vm.PlayersResultViewModel!.PlayerResultContextList[0];
            karte.CorrectionSuggestionCommand.Execute(null);

            Assert.IsFalse(vm.PlayersResultViewModel.HatOffeneBewertungen);
        }
    }
}
