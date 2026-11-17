using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Ein gespieltes Spiel löschen.
    /// <para>
    /// Im Gedächtnis stand seit dem 2026-08-24: <i>„Ein gespieltes Spiel lässt sich gar nicht
    /// löschen"</i> - die Kaskade auf <c>GameGridCoordinate</c> laufe in das NO ACTION von
    /// <c>QuestionResult</c>. Nachgemessen am 2026-09-06: <c>GamesController.BeforeActionAsync</c>
    /// räumt vor dem Löschen selbst auf (Zuordnungen, Ergebnisse, Zellen), und zwar am geteilten
    /// DataContext. Der Weg funktioniert also. Diese Zusicherung hält ihn fest.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameDeletionUnitTests
    {
        private Category kategorie = null!;
        private QuestionBase frage = null!;
        private Player spieler = null!;
        private Game spiel = null!;
        private GameGridCoordinate zelle = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            kategorie = new Category { Id = Guid.NewGuid(), Designation = "Loeschen" };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Frage zum Loeschen",
                DesignationShort = "L",
                CategoryId = kategorie.Id,
                Points = 100,
            };

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            spieler = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(spieler);
                await ctrl.SaveChangesAsync();
            }

            spiel = new Game { Id = Guid.NewGuid(), Designation = "Gespielt", Phase = 1, SuggestedPhases = 1 };

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                X = 0,
                Y = 0,
                Phase = 1,
                QuestionBaseId = frage.Id,
                IsDone = true,
            };

            using (var ctrlZellen = new GameGridCoordinatesController())
            using (var ctrlLink = new PlayerXGamesController(ctrlZellen))
            using (var ctrlErgebnis = new QuestionResultsController(ctrlZellen))
            {
                await ctrlZellen.InsertAsync(zelle);

                await ctrlLink.InsertAsync(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    PlayerId = spieler.Id,
                });

                await ctrlErgebnis.InsertAsync(new QuestionResult
                {
                    Id = Guid.NewGuid(),
                    PlayerId = spieler.Id,
                    GameId = spiel.Id,
                    GameGridCoordinateId = zelle.Id,
                    QuestionBaseId = frage.Id,
                    Score = 300,
                });

                await ctrlZellen.SaveChangesAsync();
            }
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                if (await ctrl.GetAsync(spiel.Id) != null)
                {
                    await ctrl.DeleteAsync(spiel.Id);
                    await ctrl.SaveChangesAsync();
                }
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

        /// <summary>
        /// Der Aufbau steht wirklich - sonst misst das Löschen darunter nichts.
        /// </summary>
        [TestMethod]
        public async Task ThePlayedGameIsReallyThere()
        {
            using var ctrl = new GamesController();

            var geladen = await ctrl.GetAsync(spiel.Id);

            Assert.IsNotNull(geladen, "Das Spiel fehlt.");
            Assert.AreEqual(1, geladen!.GameGridCoordinates.Count, "Die Zelle fehlt.");

            using var ctrlErgebnis = new QuestionResultsController();

            var ergebnisse = await ctrlErgebnis.GetAllResultsForCoordinate(zelle.Id);

            Assert.AreEqual(1, ergebnisse?.Count ?? 0,
                "Das Ergebnis fehlt - dann ist das Spiel gar nicht gespielt.");
        }

        /// <summary>
        /// Ein gespieltes Spiel lässt sich löschen, und es nimmt seine Zellen und Ergebnisse mit.
        /// </summary>
        [TestMethod]
        public async Task APlayedGameCanBeDeletedTogetherWithItsResults()
        {
            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new GamesController();

            Assert.IsNull(await pruefer.GetAsync(spiel.Id), "Das Spiel steht noch da.");

            using var ctrlZellen = new GameGridCoordinatesController();

            Assert.IsNull(await ctrlZellen.GetAsync(zelle.Id),
                "Die Zelle des geloeschten Spiels steht noch da.");

            using var ctrlErgebnis = new QuestionResultsController();

            var uebrig = await ctrlErgebnis.GetAllResultsForCoordinate(zelle.Id);

            Assert.AreEqual(0, uebrig?.Count ?? 0,
                "Ergebniszeilen des geloeschten Spiels sind uebrig geblieben.");
        }

        /// <summary>
        /// Und der Mitspieler überlebt es - gelöscht wird das Spiel, nicht die Person.
        /// </summary>
        [TestMethod]
        public async Task DeletingTheGameLeavesThePlayer()
        {
            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using var ctrlSpieler = new PlayersController();

            Assert.IsNotNull(await ctrlSpieler.GetAsync(spieler.Id),
                "Mit dem Spiel verschwand auch der Mitspieler.");
        }

        /// <summary>
        /// M35 C7: ein Spiel mit offenem und eingelöstem Joker lässt sich löschen. Beide hängen mit NO ACTION
        /// an Spiel bzw. Zelle - ohne den Aufräumschritt scheiterte schon das Löschen der Zellen.
        /// </summary>
        [TestMethod]
        public async Task AGameWithOpenAndRedeemedJokersCanBeDeleted()
        {
            var offen = new PlayerJoker { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Kind = JokerKind.Double, Reason = "offen" };
            var eingeloest = new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Kind = JokerKind.Shield, Reason = "eingelöst",
                GameGridCoordinateId = zelle.Id, RedeemedAt = DateTimeOffset.Now,
            };

            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.InsertAsync(offen);
                await ctrl.InsertAsync(eingeloest);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using var spiele = new GamesController();
            using var pruefer = new PlayerJokersController();

            Assert.IsNull(await spiele.GetAsync(spiel.Id), "Das Spiel steht noch da.");
            Assert.AreEqual(0, (await pruefer.GetForGameAsync(spiel.Id)).Count, "Joker des gelöschten Spiels sind übrig.");
        }

        /// <summary>
        /// M37 T1: ein Teamspiel lässt sich löschen. Das Team hängt mit NO ACTION am Spiel, Platz und Ergebnis mit NO
        /// ACTION am Team - ohne den Aufräumschritt scheiterte das Löschen des Spiels am Team.
        /// </summary>
        [TestMethod]
        public async Task ATeamGameWithAssignedPlayersAndStampedResultsCanBeDeleted()
        {
            var team = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(team);
                await ctrl.SaveChangesAsync();
            }

            await using (var context = new Quizzer.Logic.Context.DataContext())
            {
                var platz = context.PlayerXGames.Single(x => x.GameId == spiel.Id);
                var ergebnis = context.QuestionResults.Single(r => r.GameId == spiel.Id);
                platz.GameTeamId = team.Id;
                ergebnis.GameTeamId = team.Id;
                await context.SaveChangesAsync();
            }

            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using var spiele = new GamesController();
            using var teams = new GameTeamsController();

            Assert.IsNull(await spiele.GetAsync(spiel.Id), "Das Spiel steht noch da.");
            Assert.AreEqual(0, (await teams.GetForGameAsync(spiel.Id)).Count, "Das Team des gelöschten Spiels ist übrig.");
        }

        /// <summary>
        /// V05 (M37 D2): scheitert das Speichern nach dem Aufräumen, steht der vorherige Zustand - Joker,
        /// Ergebnis, Zelle und Platz, nicht nur das Spiel. Die Störung ist eine Zelle zu einem Spiel, das es
        /// nicht gibt: ihr Fremdschlüssel scheitert im selben Speichern.
        /// </summary>
        [TestMethod]
        public async Task AFailedDeleteLeavesJokersAndResultsInPlace()
        {
            var joker = new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Kind = JokerKind.Shield, Reason = "eingelöst",
                GameGridCoordinateId = zelle.Id, RedeemedAt = DateTimeOffset.Now,
            };

            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.InsertAsync(joker);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new GamesController())
            using (var stoerung = new GameGridCoordinatesController(ctrl))
            {
                await ctrl.DeleteAsync(spiel.Id);
                await stoerung.InsertAsync(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Phase = 1 });

                await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    async () => await ctrl.SaveChangesAsync(),
                    "Die Störung hätte das Speichern scheitern lassen müssen - sonst misst dieser Test nichts.");
            }

            TestDatabase.ClearDiscardedChanges();

            using var spiele = new GamesController();
            var geladen = await spiele.GetAsync(spiel.Id);
            Assert.IsNotNull(geladen, "Das Spiel ist weg.");
            Assert.AreEqual(1, geladen!.GameGridCoordinates.Count, "Die Zelle ist weg, das Spiel steht noch.");
            Assert.AreEqual(1, geladen.PlayerXGames.Count, "Der Platz des Mitspielers ist weg, das Spiel steht noch.");

            using var ergebnisse = new QuestionResultsController();
            Assert.AreEqual(1, (await ergebnisse.GetAllResultsForCoordinate(zelle.Id))?.Count ?? 0, "Das Ergebnis ist weg, das Spiel steht noch.");

            using var jokerCtrl = new PlayerJokersController();
            Assert.AreEqual(1, (await jokerCtrl.GetForGameAsync(spiel.Id)).Count, "Der Joker ist weg, das Spiel steht noch.");
        }
    }
}
