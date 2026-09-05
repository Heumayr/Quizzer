using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.HelperViewModels;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Das Verkleinern eines Spielfelds, in dem schon gespielt wurde.
    /// <para>
    /// Ergebniszeilen verweisen mit NO ACTION auf ihre Zelle - das Loeschen scheitert also in der
    /// Datenbank. Bis 2026-09-06 kam dabei ein roher Fremdschluesselfehler heraus, und weil
    /// Breite und Hoehe im Speicher schon geaendert waren, liess sich das Spiel danach nicht mehr
    /// starten.
    /// </para>
    /// </summary>
    [TestClass]
    public class GridShrinkGuardUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Legt ein Ergebnis auf die Zelle des Testspiels.</summary>
        private async Task AddResultAsync()
        {
            using var ctrl = new QuestionResultsController();

            await ctrl.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(),
                PlayerId = world.Players[0].Id,
                GameId = world.Game.Id,
                GameGridCoordinateId = world.Coordinate.Id,
                QuestionBaseId = world.Question.Id,
                Score = 100,
            });

            await ctrl.SaveChangesAsync();
        }

        /// <summary>
        /// Der gemeldete Fall: das Raster wird so verkleinert, dass die gespielte Zelle
        /// herausfaellt. Statt eines Datenbankfehlers kommt eine Meldung, die sagt, was zu tun ist.
        /// </summary>
        [TestMethod]
        public async Task ShrinkingOverAPlayedCellIsRefusedWithAClearMessage()
        {
            await AddResultAsync();

            // Die Zelle liegt auf 0/0 - ein Raster ohne Spalten laesst sie herausfallen.
            world.Game.Width = 0;
            world.Game.Height = 0;

            var fehler = await Assert.ThrowsExactlyAsync<GridShrinkBlockedException>(
                () => GridBuilder.RebuildCells(world.Game, CellView.Build));

            Assert.AreEqual(1, fehler.PlayedCells, "Die Zahl der gespielten Zellen stimmt nicht.");

            StringAssert.Contains(fehler.Message, "zurücksetzen",
                "Die Meldung sagt nicht, wie der Spielleiter weiterkommt.");
        }

        /// <summary>Und die Zelle steht danach noch in der Datenbank.</summary>
        [TestMethod]
        public async Task TheePlayedCellSurvivesTheRefusedShrink()
        {
            await AddResultAsync();

            world.Game.Width = 0;
            world.Game.Height = 0;

            try
            {
                await GridBuilder.RebuildCells(world.Game, CellView.Build);
            }
            catch (GridShrinkBlockedException)
            {
                // erwartet
            }

            using var ctrl = new GameGridCoordinatesController();
            var geblieben = await ctrl.GetAsync(world.Coordinate.Id);

            Assert.IsNotNull(geblieben, "Die gespielte Zelle wurde trotzdem geloescht.");
        }

        /// <summary>
        /// Die Gegenrichtung, und sie ist die wichtigere: ohne Ergebnisse muss sich das Raster
        /// weiterhin verkleinern lassen. Ein Riegel, der immer sperrt, waere schlimmer als der
        /// Fehler.
        /// </summary>
        [TestMethod]
        public async Task WithoutResultsTheGridStillShrinks()
        {
            world.Game.Width = 0;
            world.Game.Height = 0;

            await GridBuilder.RebuildCells(world.Game, CellView.Build);

            using var ctrl = new GameGridCoordinatesController();
            var geblieben = await ctrl.GetAsync(world.Coordinate.Id);

            Assert.IsNull(geblieben, "Die leere Zelle haette wegfallen muessen.");
        }
    }
}
