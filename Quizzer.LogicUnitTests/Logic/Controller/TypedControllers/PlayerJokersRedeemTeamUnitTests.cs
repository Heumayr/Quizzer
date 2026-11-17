using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Der zweite Riegel im Teamspiel (M37 T4): <see cref="PlayerJokersController.RedeemAsync"/> verwirft eine Sperre gegen
    /// einen aus dem eigenen Team selbst, ohne die Joker-Runde am Server. Ein Spieler ohne Team ist sein eigenes; ohne
    /// Teamspiel zählt keine Zuordnung.
    /// </summary>
    [TestClass]
    public class PlayerJokersRedeemTeamUnitTests
    {
        private static readonly DateTimeOffset Jetzt = new(2026, 11, 17, 20, 0, 0, TimeSpan.FromHours(1));

        private Game spiel = null!;
        private Game anderes = null!;
        private GameGridCoordinate zelle = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private GameTeam fremd = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamjoker", Phase = 1, SuggestedPhases = 1, IsTeamGame = true };
            anderes = new Game { Id = Guid.NewGuid(), Designation = "Anderes Teamspiel", Phase = 1, SuggestedPhases = 1, IsTeamGame = true };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1 };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            fremd = new GameTeam { Id = Guid.NewGuid(), GameId = anderes.Id, Name = "Fremd", ColorIndex = 1 };

            using var ctrlSpieler = new PlayersController();
            using var ctrlSpiel = new GamesController(ctrlSpieler);
            using var ctrlZelle = new GameGridCoordinatesController(ctrlSpieler);
            using var ctrlTeams = new GameTeamsController(ctrlSpieler);
            using var ctrlPlatz = new PlayerXGamesController(ctrlSpieler);

            foreach (var p in new[] { anna, bert, carl, dora })
                await ctrlSpieler.InsertAsync(p);

            await ctrlSpiel.InsertAsync(spiel);
            await ctrlSpiel.InsertAsync(anderes);
            await ctrlZelle.InsertAsync(zelle);

            foreach (var t in new[] { rot, blau, fremd })
                await ctrlTeams.InsertAsync(t);

            // Anna und Bert sind Rot, Carl ist Blau, Dora ohne Team.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (dora, (GameTeam?)null) })
                await ctrlPlatz.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = p.Id, GameTeamId = team?.Id });

            await ctrlSpieler.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                foreach (var id in new[] { spiel.Id, anderes.Id })
                {
                    if (await ctrl.GetAsync(id) != null)
                        await ctrl.DeleteAsync(id);
                }

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                foreach (var id in new[] { anna.Id, bert.Id, carl.Id, dora.Id })
                {
                    if (await ctrl.GetAsync(id) != null)
                        await ctrl.DeleteAsync(id);
                }

                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        private static Player Spieler(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        /// <summary>Vergibt eine Sperre und löst sie gegen <paramref name="ziel"/> ein, geschrieben wie im Spiel.</summary>
        private async Task<JokerRedeemOutcome> SperreAsync(Player wer, Player ziel)
        {
            using var ctrl = new PlayerJokersController();
            var joker = await ctrl.GrantAsync(spiel.Id, wer.Id, JokerKind.Block, "verdient", Jetzt.AddHours(-1));
            await ctrl.SaveChangesAsync();

            var ergebnis = await ctrl.RedeemAsync(new JokerRedemption(joker.Id, wer.Id, spiel.Id, zelle.Id, ziel.Id), Jetzt);
            await ctrl.SaveChangesAsync();
            return ergebnis;
        }

        private static async Task SetzeAsync(Guid spielId, Player wer, GameTeam? team)
        {
            await using var context = new Quizzer.Logic.Context.DataContext();
            context.PlayerXGames.Single(x => x.GameId == spielId && x.PlayerId == wer.Id).GameTeamId = team?.Id;
            await context.SaveChangesAsync();
        }

        [TestMethod]
        public async Task ABlockAgainstATeammateIsRejected()
        {
            Assert.AreEqual(JokerRedeemOutcome.TargetIsTeammate, await SperreAsync(anna, bert), "Anna hat ihren Teamkollegen Bert gesperrt.");
        }

        [TestMethod]
        public async Task ABlockAgainstAnOpponentIsRedeemed()
        {
            Assert.AreEqual(JokerRedeemOutcome.Redeemed, await SperreAsync(anna, carl), "Die Sperre gegen Carl aus dem anderen Team wurde verworfen.");
        }

        [TestMethod]
        public async Task APlayerWithoutTeamIsEveryonesOpponent()
        {
            Assert.AreEqual(JokerRedeemOutcome.Redeemed, await SperreAsync(dora, anna), "Dora ohne Team darf Anna nicht sperren.");
            Assert.AreEqual(JokerRedeemOutcome.Redeemed, await SperreAsync(bert, dora), "Bert darf Dora ohne Team nicht sperren.");
        }

        [TestMethod]
        public async Task WithoutATeamGameTheAssignmentDoesNotCount()
        {
            await using (var context = new Quizzer.Logic.Context.DataContext())
            {
                context.Games.Single(g => g.Id == spiel.Id).IsTeamGame = false;
                await context.SaveChangesAsync();
            }

            Assert.AreEqual(JokerRedeemOutcome.Redeemed, await SperreAsync(anna, bert), "Ohne Teamspiel ist Bert als Teamkollege geschützt.");
        }

        /// <summary>Ein Team, das nicht zu diesem Spiel gehört, macht niemanden zu Teamkollegen - wie <c>Teamzuordnung.TeamVon</c>.</summary>
        [TestMethod]
        public async Task ATeamOfAnotherGameMakesNoTeammates()
        {
            await SetzeAsync(spiel.Id, anna, fremd);
            await SetzeAsync(spiel.Id, bert, fremd);

            Assert.AreEqual(JokerRedeemOutcome.Redeemed, await SperreAsync(anna, bert));
        }
    }
}
