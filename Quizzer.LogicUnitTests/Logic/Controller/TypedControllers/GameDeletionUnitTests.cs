using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
