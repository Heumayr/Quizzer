using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Joker im Spielleiterfenster (M35 C7a): vergeben und zurücknehmen schreiben das Erwartete, ein
    /// eingelöster Joker bleibt stehen.
    /// </summary>
    [TestClass]
    public class JokerVergabeUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private async Task<JokerViewModel> GeladenAsync()
        {
            var vm = new JokerViewModel { GameId = world.Game.Id };
            await vm.LoadForTestAsync();
            return vm;
        }

        private static Task AusfuehrenAsync(System.Windows.Input.ICommand befehl, object? parameter = null)
            => ((AsyncRelayCommand)befehl).ExecuteAsync(parameter);

        private async Task<List<PlayerJoker>> ImSpielAsync()
        {
            using var ctrl = new PlayerJokersController();
            return await ctrl.GetForGameAsync(world.Game.Id);
        }

        [TestMethod]
        public async Task GrantingWritesTheChosenJokerForTheChosenPlayer()
        {
            var vm = await GeladenAsync();
            var bert = vm.Players.Single(p => p.Player.Id == world.Players[1].Id);

            vm.SelectedPlayer = bert;
            vm.SelectedKind = vm.Kinds.Single(k => k.Kind == JokerKind.Shield);
            vm.Reason = "Bluff gewonnen";
            var vorher = DateTimeOffset.Now.AddSeconds(-1);

            await AusfuehrenAsync(vm.GrantCommand);

            var joker = (await ImSpielAsync()).Single();
            Assert.AreEqual((bert.Player.Id, JokerKind.Shield, "Bluff gewonnen"), (joker.PlayerId, joker.Kind, joker.Reason));
            Assert.IsTrue(joker.GrantedAt >= vorher && joker.GrantedAt <= DateTimeOffset.Now.AddSeconds(1), "Vergabezeit fehlt.");
            Assert.IsFalse(joker.IsRedeemed);
            Assert.AreEqual(1, vm.Players.Single(p => p.Player.Id == bert.Player.Id).Jokers.Count, "Die Liste zeigt den neuen Joker nicht.");
        }

        [TestMethod]
        public async Task ATooLongReasonWritesNothing()
        {
            var vm = await GeladenAsync();
            vm.SelectedPlayer = vm.Players[0];
            vm.Reason = new string('x', PlayerJoker.MaxReasonLength + 1);

            await AusfuehrenAsync(vm.GrantCommand);

            Assert.AreEqual(0, (await ImSpielAsync()).Count, "Ein Joker mit zu langem Grund wurde geschrieben.");
            StringAssert.Contains(vm.Message, PlayerJoker.MaxReasonLength.ToString());
        }

        [TestMethod]
        public async Task WithdrawingRemovesAnOpenJoker()
        {
            var vm = await GeladenAsync();
            vm.SelectedPlayer = vm.Players[0];
            vm.Reason = "aus Versehen";
            await AusfuehrenAsync(vm.GrantCommand);

            var eintrag = vm.Players[0].Jokers.Single();
            Assert.IsTrue(vm.WithdrawCommand.CanExecute(eintrag), "Ein offener Joker lässt sich nicht zurücknehmen.");

            await AusfuehrenAsync(vm.WithdrawCommand, eintrag);

            Assert.AreEqual(0, (await ImSpielAsync()).Count, "Der zurückgenommene Joker steht noch in der Datenbank.");
            Assert.AreEqual(1, prompt.Confirmations.Count, "Zurückgenommen wurde ohne Rückfrage.");
            Assert.AreEqual(0, vm.Players[0].Jokers.Count);
        }

        [TestMethod]
        public async Task ARedeemedJokerCannotBeWithdrawn()
        {
            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.InsertAsync(new PlayerJoker
                {
                    Id = Guid.NewGuid(), GameId = world.Game.Id, PlayerId = world.Players[0].Id, Kind = JokerKind.Double,
                    Reason = "schon gespielt", GrantedAt = DateTimeOffset.Now,
                    GameGridCoordinateId = world.Coordinate.Id, RedeemedAt = DateTimeOffset.Now,
                });
                await ctrl.SaveChangesAsync();
            }

            var vm = await GeladenAsync();
            var eintrag = vm.Players.Single(p => p.Player.Id == world.Players[0].Id).Jokers.Single();

            Assert.IsFalse(vm.WithdrawCommand.CanExecute(eintrag), "Der Knopf bietet das Zurücknehmen eines eingelösten Jokers an.");

            // Am Knopf vorbei: auch dann bleibt er stehen.
            await AusfuehrenAsync(vm.WithdrawCommand, eintrag);

            Assert.AreEqual(1, (await ImSpielAsync()).Count, "Der eingelöste Joker wurde zurückgenommen.");
        }
    }
}
