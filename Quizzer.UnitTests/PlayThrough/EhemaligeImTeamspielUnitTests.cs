using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
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

        /// <summary>M37 P6, P4-1: „Wer spielt mit?“ sagt im Teamspiel, dass die gebuchten Punkte beim Team bleiben - ohne Teamspiel wie bisher.</summary>
        [TestMethod]
        public async Task TheQuestionWhoPlaysSaysWhereThePointsGo()
        {
            await OeffneAsync(anna: ScoreSuggestion.Right);

            var team = await RueckfrageBeimAbwaehlenAsync(teamspiel: true);
            StringAssert.Contains(team, "bleiben die schon gebuchten Punkte beim Team", "Im Teamspiel sagt die Rückfrage nicht, wohin die Punkte gehen.");
            Assert.IsFalse(team.Contains("aus der Wertung"), "Im Teamspiel droht die Rückfrage mit einem Verlust, den es seit R4-4 nicht gibt: " + team);

            StringAssert.Contains(await RueckfrageBeimAbwaehlenAsync(teamspiel: false), "Ohne Haken fällt das aus der Wertung heraus.");
        }

        /// <summary>
        /// M37 P8, P6-4: eine leere Ergebniszeile (Zelle geöffnet, nichts gebucht) sind keine Punkte - „Wer spielt mit?“ fragt
        /// beim Abwählen nicht nach, in beiden Betriebsarten. Das Maß ist <see cref="Teamwertung.OhneBuchung"/>.
        /// </summary>
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task AnUnbookedRowIsNoPointsForTheQuestionWhoPlays(bool teamspiel)
        {
            await OeffneAsync();

            using (var ctrl = new QuestionResultsController())
            {
                var zeilen = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? [];
                Assert.IsTrue(zeilen.Any(z => z.PlayerId == world.Players[0].Id && Teamwertung.OhneBuchung(z)),
                    "Anna hat keine leere Zeile in der Datenbank - dann misst der Test nichts.");
            }

            await using (var context = new Quizzer.Logic.Context.DataContext())
            {
                context.Games.Single(g => g.Id == world.Game.Id).IsTeamGame = teamspiel;
                await context.SaveChangesAsync();
            }

            var prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;

            var vm = new Quizzer.Views.GameViews.GamePlayersViewModel { GameId = world.Game.Id };
            await vm.LoadForTestAsync();
            vm.Choices.Single(c => c.Player.Id == world.Players[0].Id).IsChecked = false;
            await ((AsyncRelayCommand)vm.ConfirmCommand).ExecuteAsync(null);

            Assert.AreEqual(0, prompt.Confirms.Count, "„Wer spielt mit?“ nennt Punkte, die nie gebucht wurden: "
                + string.Join(" | ", prompt.Confirms.Select(c => c.Message)));
            Assert.IsTrue(vm.Confirmed, "Ohne Rückfrage ging es nicht weiter.");
        }

        /// <summary>„Wer spielt mit?“ ohne Anna; die Rückfrage wird verneint, es bleibt alles, wie es ist.</summary>
        private async Task<string> RueckfrageBeimAbwaehlenAsync(bool teamspiel)
        {
            await using (var context = new Quizzer.Logic.Context.DataContext())
            {
                context.Games.Single(g => g.Id == world.Game.Id).IsTeamGame = teamspiel;
                await context.SaveChangesAsync();
            }

            var prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;

            var vm = new Quizzer.Views.GameViews.GamePlayersViewModel { GameId = world.Game.Id };
            await vm.LoadForTestAsync();
            vm.Choices.Single(c => c.Player.Id == world.Players[0].Id).IsChecked = false;
            await ((AsyncRelayCommand)vm.ConfirmCommand).ExecuteAsync(null);

            Assert.IsFalse(vm.Confirmed, "Trotz verneinter Rückfrage ging es weiter.");
            return prompt.Confirms.Single().Message;
        }
    }
}
