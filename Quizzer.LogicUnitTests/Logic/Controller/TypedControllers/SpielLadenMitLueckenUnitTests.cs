using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Ein Spiel laden, in dem etwas fehlt - eine Zelle ohne Frage, ein entfernter Mitspieler.
    /// <para>
    /// <c>GamesController.AfterActionAsync</c> setzt nach dem Laden die Rückverweise. Bis
    /// 2026-09-07 stand daneben dreimal <c>//must be existend!</c> - bei <c>Player</c> ist das
    /// <b>nachweislich falsch</b>: wer aus dem Spiel genommen wurde, steht nicht mehr in der
    /// Mannschaft, seine Ergebniszeilen bleiben aber. In der Spieldatenbank lagen drei solche
    /// Zeilen, und „Nächster wählt aus" fiel darüber mit einer NullReferenceException.
    /// </para>
    /// </summary>
    [TestClass]
    public class SpielLadenMitLueckenUnitTests
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

            kategorie = new Category { Id = Guid.NewGuid(), Designation = $"Kat-{Guid.NewGuid():N}" };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Frage",
                DesignationShort = "F",
                CategoryId = kategorie.Id,
                Points = 100,
            };

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            spieler = new Player { Id = Guid.NewGuid(), Designation = "Weg", DisplayName = "Weg" };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(spieler);
                await ctrl.SaveChangesAsync();
            }

            spiel = new Game { Id = Guid.NewGuid(), Designation = "Luecken", Phase = 1, SuggestedPhases = 1 };

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
                QuestionBaseId = frage.Id,
            };

            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(zelle);
                await ctrl.SaveChangesAsync();
            }
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
        }

        private async Task LegeErgebnisAnAsync()
        {
            using var ctrl = new QuestionResultsController();

            await ctrl.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(),
                PlayerId = spieler.Id,
                GameId = spiel.Id,
                GameGridCoordinateId = zelle.Id,
                QuestionBaseId = frage.Id,
                Score = 100,
                CorrectAnswered = true,
            });

            await ctrl.SaveChangesAsync();
        }

        /// <summary>
        /// <b>Eine Ergebniszeile eines Mitspielers, der nicht mehr in der Mannschaft steht,
        /// verhindert das Laden nicht - und ihr <c>Player</c> ist dann <c>null</c>.</b>
        /// <para>
        /// Genau darauf müssen sich die Leser einstellen; der Kommentar am Quelltext behauptete
        /// bis 2026-09-07 das Gegenteil.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AResultOfSomebodyOutsideTheRosterLoadsWithANullPlayer()
        {
            await LegeErgebnisAnAsync();

            using var ctrl = new GamesController();

            var geladen = await ctrl.GetAsync(spiel.Id);

            Assert.IsNotNull(geladen, "Das Spiel liess sich nicht laden.");

            var ergebnis = geladen!.QuestionResults.SingleOrDefault();

            Assert.IsNotNull(ergebnis, "Die Ergebniszeile kam nicht mit - dann misst die Probe nichts.");

            Assert.IsNull(ergebnis!.Player,
                "Der Rueckverweis zeigt auf einen Mitspieler, der gar nicht in der Mannschaft "
                + "steht - dann waere der Kommentar 'must be existend' doch richtig, und diese "
                + "Probe misst etwas anderes als gemeint.");
        }

        /// <summary>
        /// <b>Eine Zelle, deren Frage entfernt wurde, lässt das Spiel weiterhin laden.</b>
        /// <para>
        /// Der Zugriff auf <c>GameGridCoordinate.QuestionBase</c> lief bis 2026-09-07 ungeprüft
        /// über den Rückverweis. Ein Wurf hier heißt: das Spiel öffnet gar nicht mehr.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task ACellWithoutAQuestionStillLoads()
        {
            await LegeErgebnisAnAsync();

            using (var ctrl = new GameGridCoordinatesController())
            {
                var geladen = await ctrl.GetAsync(zelle.Id);

                geladen!.QuestionBaseId = null;

                await ctrl.UpdateAsync(geladen);
                await ctrl.SaveChangesAsync();
            }

            using var ctrlSpiel = new GamesController();

            var spielstand = await ctrlSpiel.GetAsync(spiel.Id);

            Assert.IsNotNull(spielstand,
                "Das Spiel laesst sich nicht mehr laden, sobald eine gespielte Zelle ihre Frage "
                + "verloren hat.");

            Assert.IsNull(spielstand!.QuestionResults.Single().QuestionBase,
                "Der Rueckverweis auf die Frage zeigt irgendwohin.");
        }
    }
}
