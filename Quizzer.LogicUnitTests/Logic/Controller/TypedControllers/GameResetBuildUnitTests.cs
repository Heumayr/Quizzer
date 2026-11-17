using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// „Spielaufbau zurücksetzen“ (V19, M37 T2). Gemessen vor der Behebung: ein eingelöster Joker an einer Zelle liess
    /// das Löschen der Zellen am NO ACTION scheitern - die Ergebnisse waren da schon weg, Zellen, Plätze und Joker
    /// standen noch (1 von 1 Ergebnis gelöscht, 1 Zelle, 2 Plätze, 1 Joker übrig).
    /// </summary>
    [TestClass]
    public class GameResetBuildUnitTests
    {
        private Category kategorie = null!;
        private QuestionBase frage = null!;
        private Player spieler = null!;
        private Game spiel = null!;
        private GameGridCoordinate zelle = null!;
        private GameTeam team = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            kategorie = new Category { Id = Guid.NewGuid(), Designation = "Zuruecksetzen" };
            frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Frage", DesignationShort = "Z", Points = 100 };
            spieler = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            spiel = new Game { Id = Guid.NewGuid(), Designation = "Aufbau", Phase = 1, SuggestedPhases = 1, IsTeamGame = true };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, Phase = 1, IsDone = true };
            team = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5, SpeakerTurn = 3 };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            frage.CategoryId = kategorie.Id;
            zelle.QuestionBaseId = frage.Id;

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(spieler);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            using var ctrlTeams = new GameTeamsController();
            using var ctrlZellen = new GameGridCoordinatesController(ctrlTeams);
            using var ctrlPlatz = new PlayerXGamesController(ctrlTeams);
            using var ctrlErgebnis = new QuestionResultsController(ctrlTeams);
            using var ctrlKopf = new HeadersController(ctrlTeams);
            using var ctrlJoker = new PlayerJokersController(ctrlTeams);

            await ctrlTeams.InsertAsync(team);
            await ctrlZellen.InsertAsync(zelle);
            await ctrlPlatz.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, GameTeamId = team.Id });
            await ctrlErgebnis.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(), PlayerId = spieler.Id, GameId = spiel.Id, GameGridCoordinateId = zelle.Id,
                QuestionBaseId = frage.Id, Score = 300, GameTeamId = team.Id,
            });
            await ctrlKopf.InsertAsync(new Header { Id = Guid.NewGuid(), GameId = spiel.Id, HeaderType = HeaderType.Column, Designation = "Kopf" });
            await ctrlJoker.InsertAsync(new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Kind = JokerKind.Shield, Reason = "eingelöst",
                GameGridCoordinateId = zelle.Id, RedeemedAt = DateTimeOffset.Now,
            });

            await ctrlTeams.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.DeleteAsync(frage.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                await ctrl.DeleteAsync(spieler.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(kategorie.Id);
                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        private sealed record Stand(int Ergebnisse, int Zellen, int Plaetze, int Kopfzeilen, int Joker, int Teams, int Sprecher);

        private async Task<Stand> MessenAsync()
        {
            using var spiele = new GamesController();
            var geladen = (await spiele.GetAsync(spiel.Id))!;

            using var joker = new PlayerJokersController();
            using var teams = new GameTeamsController();
            var teamListe = await teams.GetForGameAsync(spiel.Id);

            return new Stand(
                geladen.QuestionResults.Count(), geladen.GameGridCoordinates.Count, geladen.PlayerXGames.Count, geladen.Headers.Count,
                (await joker.GetForGameAsync(spiel.Id)).Count, teamListe.Count, teamListe.Sum(t => t.SpeakerTurn));
        }

        /// <summary>
        /// Mit eingelöstem Joker an der Zelle: alles vom Aufbau geht, die Teams bleiben mit Name und Farbe, ihr
        /// Sprecherstand geht auf den Anfang. Ohne das Räumen der Joker scheitert es am NO ACTION der Zelle.
        /// </summary>
        [TestMethod]
        public async Task ResettingWithARedeemedJokerClearsTheBuildAndKeepsTheTeams()
        {
            Assert.AreEqual(new Stand(1, 1, 1, 1, 1, 1, 3), await MessenAsync(), "Der Aufbau steht nicht - dann misst das Zurücksetzen nichts.");

            using (var ctrl = new GamesController())
            {
                await ctrl.ResetBuildAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            Assert.AreEqual(new Stand(0, 0, 0, 0, 0, 1, 0), await MessenAsync());

            using var teams = new GameTeamsController();
            var geblieben = (await teams.GetForGameAsync(spiel.Id)).Single();
            Assert.AreEqual(("Rot", 5), (geblieben.Name, geblieben.ColorIndex), "Das Team hat Name oder Farbe verloren.");
        }

        /// <summary>
        /// Scheitert das Speichern, steht der Aufbau wie vorher - auch die Ergebnisse, die vor den Zellen gehen. Die
        /// Störung ist eine Zelle zu einem Spiel, das es nicht gibt (Muster <c>GameDeletionUnitTests</c>).
        /// </summary>
        [TestMethod]
        public async Task AFailedResetLeavesTheBuildInPlace()
        {
            using (var ctrl = new GamesController())
            using (var stoerung = new GameGridCoordinatesController(ctrl))
            {
                await ctrl.ResetBuildAsync(spiel.Id);
                await stoerung.InsertAsync(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Phase = 1 });

                await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    async () => await ctrl.SaveChangesAsync(),
                    "Die Störung hätte das Speichern scheitern lassen müssen - sonst misst dieser Test nichts.");
            }

            TestDatabase.ClearDiscardedChanges();

            Assert.AreEqual(new Stand(1, 1, 1, 1, 1, 1, 3), await MessenAsync(), "Ein gescheitertes Zurücksetzen hat schon etwas weggenommen.");
        }
    }
}
