using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller
{
    /// <summary>
    /// Mehrere Controller an einem geteilten <c>DataContext</c> schreiben in einem Vorgang.
    /// <para>
    /// Der verkettende Konstruktor <c>ControllerBase(other)</c> ist die Klammer, die dieses
    /// Projekt fuer zusammengehoerige Schreibvorgaenge vorsieht. Ob sie wirklich klammert, war
    /// nie gemessen - und <c>EditGameViewModel.VMSaveAsync</c> schrieb Spiel, Kopfzeilen und
    /// Zellen bis 2026-09-06 in drei getrennten Vorgaengen: scheiterte der dritte, war der
    /// erste schon festgeschrieben.
    /// </para>
    /// </summary>
    [TestClass]
    public class SharedContextTransactionUnitTests
    {
        private Game spiel = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = "Ausgangsstand",
                Phase = 1,
                SuggestedPhases = 1,
            };

            using var ctrl = new GamesController();

            await ctrl.InsertAsync(spiel);
            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using var ctrl = new GamesController();

            await ctrl.DeleteAsync(spiel.Id);
            await ctrl.SaveChangesAsync();

            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>Eine Zelle, deren Frage es nicht gibt - der Fremdschluessel schlaegt fehl.</summary>
        private GameGridCoordinate UnschreibbareZelle() => new()
        {
            Id = Guid.NewGuid(),
            GameId = spiel.Id,
            X = 0,
            Y = 0,
            Phase = 1,
            QuestionBaseId = Guid.NewGuid(),
        };

        private async Task<string?> BezeichnungAusDerDatenbankAsync()
        {
            using var ctrl = new GamesController();

            return (await ctrl.GetAsync(spiel.Id))?.Designation;
        }

        /// <summary>
        /// Der gute Fall: verkettete Controller schreiben alles, was sie zusammen aendern.
        /// Ohne diese Zusicherung waere die naechste auch dann gruen, wenn die Klammer
        /// ueberhaupt nichts mehr schreibt.
        /// </summary>
        [TestMethod]
        public async Task ChainedControllersWriteEverythingTogether()
        {
            var zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                X = 1,
                Y = 1,
                Phase = 1,
            };

            using (var ctrlGames = new GamesController())
            using (var ctrlCells = new GameGridCoordinatesController(ctrlGames))
            {
                spiel.Designation = "Zusammen geschrieben";

                await ctrlGames.UpdateAsync(spiel);
                await ctrlCells.InsertAsync(zelle);

                // Eine einzige Sicherung fuer beide Controller.
                await ctrlGames.SaveChangesAsync();
            }

            Assert.AreEqual("Zusammen geschrieben", await BezeichnungAusDerDatenbankAsync(),
                "Die Aenderung am Spiel ist nicht angekommen.");

            using var ctrlPruef = new GameGridCoordinatesController();

            Assert.IsNotNull(await ctrlPruef.GetAsync(zelle.Id),
                "Die Zelle ist nicht angekommen - dann klammert die Verkettung nicht, sie schluckt.");

            await ctrlPruef.DeleteAsync(zelle.Id);
            await ctrlPruef.SaveChangesAsync();
        }

        /// <summary>
        /// Der Fall, um den es geht: scheitert einer der Schreibvorgaenge, darf keiner stehen
        /// bleiben.
        /// </summary>
        [TestMethod]
        public async Task AFailureInOneWriteRollsBackTheOthers()
        {
            using (var ctrlGames = new GamesController())
            using (var ctrlCells = new GameGridCoordinatesController(ctrlGames))
            {
                spiel.Designation = "Darf nicht stehenbleiben";

                await ctrlGames.UpdateAsync(spiel);
                await ctrlCells.InsertAsync(UnschreibbareZelle());

                await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    async () => await ctrlGames.SaveChangesAsync(),
                    "Die unschreibbare Zelle haette scheitern muessen - sonst misst dieser Test nichts.");
            }

            Assert.AreEqual("Ausgangsstand", await BezeichnungAusDerDatenbankAsync(),
                "Die Aenderung am Spiel ist trotz des Fehlers festgeschrieben worden.");

            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>
        /// Die Gegenrichtung, und zugleich der Beleg fuer den behobenen Fehler: **ohne**
        /// Verkettung bleibt der erste Schreibvorgang stehen, wenn der zweite scheitert.
        /// Genau so schrieb der Spieleditor bis 2026-09-06.
        /// </summary>
        [TestMethod]
        public async Task WithoutChainingTheFirstWriteSurvivesTheSecondFailure()
        {
            using (var ctrlGames = new GamesController())
            {
                spiel.Designation = "Bleibt stehen";

                await ctrlGames.UpdateAsync(spiel);
                await ctrlGames.SaveChangesAsync();
            }

            using (var ctrlCells = new GameGridCoordinatesController())
            {
                await ctrlCells.InsertAsync(UnschreibbareZelle());

                await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    async () => await ctrlCells.SaveChangesAsync());
            }

            Assert.AreEqual("Bleibt stehen", await BezeichnungAusDerDatenbankAsync(),
                "Ohne Verkettung muesste der erste Vorgang festgeschrieben sein - ist er das "
                + "nicht, misst der Vergleich mit der verketteten Fassung nichts.");

            TestDatabase.ClearDiscardedChanges();
        }
    }
}
