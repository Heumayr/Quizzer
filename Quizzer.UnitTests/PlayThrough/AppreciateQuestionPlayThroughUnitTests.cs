using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews.Sub;
using System.Collections.Concurrent;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Eine Schaetzfrage von der Eingabe bis zur Wertung: die Spieler tippen einen Wert, und das
    /// Spiel bestimmt selbst, wer am naechsten dran liegt.
    /// <para>
    /// Die Tipps werden hier direkt eingespeist, ohne SignalR - geprueft wird der Weg vom
    /// abgegebenen Wert bis zum Bewertungsvorschlag im Ergebnisfenster.
    /// </para>
    /// </summary>
    [TestClass]
    public class AppreciateQuestionPlayThroughUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static async Task<(TestGameBuilder World, TestableCurrentQuestionViewModel Vm)>
            OpenAppreciateAsync(Action<AppreciateQestion> configure, int playerCount = 3)
        {
            var world = await TestGameBuilder.CreateAsync(
                questionType: QuestionType.Appreciate, normalStepCount: 0, playerCount: playerCount);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            configure((AppreciateQestion)vm.Coordinate!.QuestionBase!);

            return (world, vm);
        }

        private static ConcurrentDictionary<Guid, InputResult> Inputs(
            TestGameBuilder world, params string[] values)
        {
            var inputs = new ConcurrentDictionary<Guid, InputResult>();

            for (var i = 0; i < values.Length && i < world.Players.Count; i++)
            {
                var player = world.Players[i];
                inputs[player.Id] = new InputResult
                {
                    PlayerId = player.Id,
                    Player = player,
                    Value = values[i],
                    CommittedResult = true,
                };
            }

            return inputs;
        }

        [TestMethod]
        public async Task AnAppreciateQuestion_UsesTheInputLayout()
        {
            var (world, vm) = await OpenAppreciateAsync(_ => { });
            await using var _ = world;

            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout,
                "Ohne das Eingabe-Layout bekaemen die Spieler nichts zu tippen.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        [TestMethod]
        public async Task TheClosestGuessIsProposedAsCorrect()
        {
            var (world, vm) = await OpenAppreciateAsync(q =>
            {
                q.ValueKind = AppreciateValueKind.Length;
                q.Unit = AppreciateUnit.Meter;
                q.ExpectedValue = 3798;
            });
            await using var _ = world;

            var outcome = vm.EvaluateGuesses(Inputs(world, "3000", "3750", "5000"));
            vm.PlayersResultViewModel!.ApplyAppreciateOutcome(
                outcome, Inputs(world, "3000", "3750", "5000"));

            var contexts = vm.PlayersResultViewModel.PlayerResultContextList;
            var winner = contexts.Single(c => c.IsAppreciateWinner);

            Assert.AreEqual(world.Players[1].Id, winner.Player.Id);
            Assert.AreEqual(PlayerResultContext.ScoreSuggestion.Right, winner.Suggestion);
        }

        /// <summary>
        /// Ein danebenliegender Tipp darf nicht ungefragt Minuspunkte kosten - der Spielleiter
        /// entscheidet das.
        /// </summary>
        [TestMethod]
        public async Task TheOthersAreNotMarkedWrongAutomatically()
        {
            var (world, vm) = await OpenAppreciateAsync(q => q.ExpectedValue = 100);
            await using var _ = world;

            var inputs = Inputs(world, "99", "500", "900");
            vm.PlayersResultViewModel!.ApplyAppreciateOutcome(vm.EvaluateGuesses(inputs), inputs);

            var losers = vm.PlayersResultViewModel.PlayerResultContextList
                .Where(c => !c.IsAppreciateWinner).ToList();

            Assert.AreEqual(2, losers.Count);
            Assert.IsTrue(losers.All(c => c.Suggestion == PlayerResultContext.ScoreSuggestion.None));
        }

        [TestMethod]
        public async Task OnATie_BothAreProposedAsCorrect()
        {
            var (world, vm) = await OpenAppreciateAsync(q => q.ExpectedValue = 100);
            await using var _ = world;

            var inputs = Inputs(world, "90", "110", "400");
            vm.PlayersResultViewModel!.ApplyAppreciateOutcome(vm.EvaluateGuesses(inputs), inputs);

            var winners = vm.PlayersResultViewModel.PlayerResultContextList
                .Where(c => c.IsAppreciateWinner).ToList();

            Assert.AreEqual(2, winners.Count);
            Assert.IsTrue(winners.All(c => c.Suggestion == PlayerResultContext.ScoreSuggestion.Right));
        }

        [TestMethod]
        public async Task AnUnreadableGuessIsShownAsSuchAndDoesNotWin()
        {
            var (world, vm) = await OpenAppreciateAsync(q => q.ExpectedValue = 100);
            await using var _ = world;

            var inputs = Inputs(world, "keine Ahnung", "150", "900");
            vm.PlayersResultViewModel!.ApplyAppreciateOutcome(vm.EvaluateGuesses(inputs), inputs);

            var contexts = vm.PlayersResultViewModel.PlayerResultContextList;
            var unreadable = contexts.Single(c => c.Player.Id == world.Players[0].Id);

            Assert.IsFalse(unreadable.IsAppreciateWinner);
            Assert.AreEqual("nicht lesbar", unreadable.AppreciateDistanceText);
            Assert.AreEqual(world.Players[1].Id, contexts.Single(c => c.IsAppreciateWinner).Player.Id);
        }

        [TestMethod]
        public async Task ADateQuestionPicksTheClosestDate()
        {
            var (world, vm) = await OpenAppreciateAsync(q =>
            {
                q.ValueKind = AppreciateValueKind.Date;
                q.Unit = AppreciateUnit.Datum;
                q.ExpectedDate = new DateTime(1969, 7, 20);
            });
            await using var _ = world;

            var inputs = Inputs(world, "1969-01-01", "1969-07-25", "1970-07-20");
            vm.PlayersResultViewModel!.ApplyAppreciateOutcome(vm.EvaluateGuesses(inputs), inputs);

            var winner = vm.PlayersResultViewModel.PlayerResultContextList
                .Single(c => c.IsAppreciateWinner);

            Assert.AreEqual(world.Players[1].Id, winner.Player.Id);
        }

        [TestMethod]
        public async Task UnitsAreConvertedBeforeComparing()
        {
            var (world, vm) = await OpenAppreciateAsync(q =>
            {
                q.ValueKind = AppreciateValueKind.Length;
                q.Unit = AppreciateUnit.Kilometer;
                q.ExpectedValue = 2;
            });
            await using var _ = world;

            // Getippt wird in km: 1,9 liegt naeher an 2 als 2,5.
            var inputs = Inputs(world, "1,9", "2,5", "10");
            vm.PlayersResultViewModel!.ApplyAppreciateOutcome(vm.EvaluateGuesses(inputs), inputs);

            var winner = vm.PlayersResultViewModel.PlayerResultContextList
                .Single(c => c.IsAppreciateWinner);

            Assert.AreEqual(world.Players[0].Id, winner.Player.Id);
        }

        [TestMethod]
        public async Task TheSummaryNamesTheExpectedValueAndTheWinner()
        {
            var (world, vm) = await OpenAppreciateAsync(q =>
            {
                q.ValueKind = AppreciateValueKind.Length;
                q.Unit = AppreciateUnit.Meter;
                q.ExpectedValue = 3798;
            });
            await using var _ = world;

            var inputs = Inputs(world, "3750", "9000", "10");
            await vm.SubmitAllForTestAsync(inputs);

            StringAssert.Contains(vm.AppreciateSummary, "3798");
            StringAssert.Contains(vm.AppreciateSummary, world.Players[0].CalculatedDisplayName);
        }

        [TestMethod]
        public async Task ASingleGuessIsShownBeforeEveryoneHasAnswered()
        {
            var (world, vm) = await OpenAppreciateAsync(q => q.ExpectedValue = 100);
            await using var _ = world;

            var single = Inputs(world, "42").Values.Single();
            vm.PlayersResultViewModel!.SetInputResult(single);

            var ctx = vm.PlayersResultViewModel.PlayerResultContextList
                .Single(c => c.Player.Id == world.Players[0].Id);

            Assert.AreEqual("42", ctx.AppreciateGuessText);
        }
    }
}
