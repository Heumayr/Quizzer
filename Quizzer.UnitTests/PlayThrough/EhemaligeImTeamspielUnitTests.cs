using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Ein Mitspieler wird im Teamspiel abgewählt („Wer spielt mit?“ löscht seinen Platz), nachdem er an einer Zelle Punkte geholt
    /// hat (R4-4). Das Aufräumen beim Wiederöffnen der Zelle nahm seinem Team diese Punkte - in der Datenbank und am Beamer.
    /// F-M37-07: Punkte bleiben beim Team ihres Stempels. Ohne Teamspiel räumt es weiter (<c>ResultRosterMismatchUnitTests</c>).
    /// </summary>
    [TestClass]
    public class EhemaligeImTeamspielUnitTests
    {
        private TestGameBuilder world = null!;
        private GameTeam rot = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 2);
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            world.Game.IsTeamGame = true;
            world.Game.GameTeams.AddRange([rot, blau]);

            await using var context = new Quizzer.Logic.Context.DataContext();
            foreach (var (spieler, team) in new[] { (world.Players[0], rot), (world.Players[1], blau) })
            {
                world.Game.PlayerXGames.Single(x => x.PlayerId == spieler.Id).GameTeamId = team.Id;
                context.PlayerXGames.Single(x => x.GameId == world.Game.Id && x.PlayerId == spieler.Id).GameTeamId = team.Id;
            }

            await context.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Öffnet die Zelle, setzt die Vorschläge, bucht sie und legt die Zeilen an die Zelle, wie das Ergebnisfenster.</summary>
        private async Task OeffneAsync(ScoreSuggestion anna = ScoreSuggestion.None)
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            await ((AsyncRelayCommand)vm.OpenResultsCommand).ExecuteAsync(null);

            if (anna != ScoreSuggestion.None)
            {
                vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world.Players[0].Id).Suggestion = anna;
                Assert.IsTrue(await vm.PlayersResultViewModel.TrySaveAsync(), "Das Buchen scheiterte.");
            }

            await vm.ClosedForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>„Wer spielt mit?“ ohne Anna: der Platz geht, in der Datenbank und im geladenen Spiel.</summary>
        private async Task WaehleAnnaAbAsync()
        {
            var platz = world.Game.PlayerXGames.Single(x => x.PlayerId == world.Players[0].Id);

            using var seats = new PlayerXGamesController();
            await seats.DeleteAsync(platz.Id);
            await seats.SaveChangesAsync();
            world.Game.PlayerXGames.Remove(platz);
        }

        private async Task<int> TeamstandInDerDatenbankAsync()
        {
            using var ctrl = new GamesController();
            return (await ctrl.GetAsync(world.Game.Id))!.Teamstand(rot.Id);
        }

        [TestMethod]
        public async Task ABookedRowOfAFormerPlayerStaysWithHisTeam()
        {
            await OeffneAsync(anna: ScoreSuggestion.Right);
            var punkte = world.Coordinate.CurrentPoints;
            Assert.AreEqual(punkte, await TeamstandInDerDatenbankAsync(), "Vor dem Abwählen hat Rot Annas Punkte nicht - dann misst der Test nichts.");

            await WaehleAnnaAbAsync();
            await OeffneAsync();

            Assert.AreEqual(punkte, await TeamstandInDerDatenbankAsync(), "Das Aufräumen hat Rot Annas Punkte in der Datenbank genommen.");
            Assert.AreEqual(punkte, world.Game.Teamstand(rot.Id), "Am Beamer fehlen Rot Annas Punkte nach dem Wiederöffnen.");
        }

        /// <summary>Eine leere Zeile eines Ehemaligen trägt nichts und geht weiter - die Zelle wächst nicht bei jedem Öffnen.</summary>
        [TestMethod]
        public async Task AnUnbookedRowOfAFormerPlayerIsStillRemoved()
        {
            await OeffneAsync();
            await WaehleAnnaAbAsync();
            await OeffneAsync();

            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? [];
            CollectionAssert.AreEqual(new[] { world.Players[1].Id }, zeilen.Select(z => z.PlayerId).ToArray(), "Die leere Zeile der Abgewählten steht noch da.");
        }
    }
}
