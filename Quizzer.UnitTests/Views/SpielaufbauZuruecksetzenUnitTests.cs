using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// „Spielaufbau zurücksetzen“ mit eingelöstem Joker (V19, M37 T2) - der Weg über den Knopf. Gemessen vor der
    /// Behebung: <c>FK_PlayerJoker_GameGridCoordinate_GameGridCoordinateId</c>, die Ergebnisse waren schon weg, Zellen,
    /// Plätze und Joker standen noch. Die Klammer selbst prüft <c>GameResetBuildUnitTests</c>.
    /// </summary>
    [TestClass]
    public class SpielaufbauZuruecksetzenUnitTests
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

        [TestMethod]
        public async Task ResettingWithARedeemedJokerWorksAndKeepsTheTeam()
        {
            await using var world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
            var team = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Blau", ColorIndex = 4 };

            using (var ctrl = new GameTeamsController())
            using (var ctrlJoker = new PlayerJokersController(ctrl))
            {
                await ctrl.InsertAsync(team);
                await ctrlJoker.InsertAsync(new PlayerJoker
                {
                    Id = Guid.NewGuid(), GameId = world.Game.Id, PlayerId = world.Players[0].Id, Kind = JokerKind.Double,
                    Reason = "eingelöst", GrantedAt = DateTimeOffset.Now, GameGridCoordinateId = world.Coordinate.Id, RedeemedAt = DateTimeOffset.Now,
                });
                await ctrl.SaveChangesAsync();
            }

            var vm = new EditGameViewModel();
            await vm.LoadModel(world.Game.Id);

            await ((AsyncRelayCommand)vm.ResetGameBuildCommand).ExecuteAsync(null);

            StringAssert.Contains(prompt.Confirms.Single().Message, "Joker", "Die Rückfrage verschweigt, dass die Joker mitgehen.");
            StringAssert.Contains(prompt.Confirms.Single().Message, "Teams bleiben", "Die Rückfrage sagt nicht, dass die Teams stehen bleiben.");

            using var zellen = new GameGridCoordinatesController();
            using var plaetze = new PlayerXGamesController();
            using var joker = new PlayerJokersController();
            using var teams = new GameTeamsController();

            Assert.AreEqual(0, (await zellen.GetAllAsync()).Count(c => c.GameId == world.Game.Id && c.QuestionBaseId != null), "Die Fragen hängen noch im Raster.");
            Assert.AreEqual(0, (await plaetze.GetAllAsync()).Count(x => x.GameId == world.Game.Id), "Die Mitspieler sind noch zugeordnet.");
            Assert.AreEqual(0, (await joker.GetForGameAsync(world.Game.Id)).Count, "Der eingelöste Joker steht noch.");
            Assert.AreEqual(1, (await teams.GetForGameAsync(world.Game.Id)).Count, "Das Team ist mit dem Aufbau gegangen.");
        }
    }
}
