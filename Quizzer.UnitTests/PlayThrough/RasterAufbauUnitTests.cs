using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.HelperViewModels;
using System.Diagnostics;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// P9-2 (Durchsicht M37, P10): der Rasteraufbau legt fehlende Zellen nach - am geladenen Spiel, wie beim Öffnen. Jede fehlt
    /// danach genau einmal nicht mehr, die alte bleibt mit ihrer Frage, ein zweiter Aufbau legt nichts nach, nichts bleibt
    /// ungespeichert. Die Dauer steht in der Ausgabe (P9 gemessen: 20×20 5,6 s, 40×40 12 s).
    /// </summary>
    [TestClass]
    public class RasterAufbauUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
            TestEnvironment.ClearDiscardedChanges();
        }

        [TestCleanup]
        public void TearDown()
        {
            UserPrompt.Reset();
            TestEnvironment.ClearDiscardedChanges();
        }

        [TestMethod]
        [DataRow(20, 20)]
        [DataRow(40, 40)]
        public async Task GrowingTheGridAddsEveryMissingCellOnce(int breite, int hoehe)
        {
            await using var world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);

            world.Game.Width = breite;
            world.Game.Height = hoehe;
            using (var ctrl = new GamesController())
            {
                await ctrl.UpdateAsync(world.Game);
                await ctrl.SaveChangesAsync();
            }

            Game spiel;
            using (var ctrl = new GamesController())
                spiel = (await ctrl.GetAsync(world.Game.Id))!;

            var uhr = Stopwatch.StartNew();
            var raster = await GridBuilder.RebuildCells(spiel, CellView.Build);
            Console.WriteLine($"RASTER {breite}x{hoehe}: {uhr.ElapsedMilliseconds} ms");
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.AreEqual(breite * hoehe, raster.CellVMs.Count, "Nicht jede Position hat eine Zelle.");
            Assert.IsTrue(raster.CellVMs.All(c => c.Coordinate.Game == spiel), "Eine Zelle hängt nicht am Spiel.");
            Assert.AreEqual((breite, hoehe), (raster.ColumnHeaderVMs.Count, raster.RowHeaderVMs.Count), "Die Köpfe fehlen.");

            var gespeichert = await ZellenAsync(spiel.Id);
            Assert.AreEqual(breite * hoehe, gespeichert.Count, "In der Datenbank fehlen Zellen oder stehen doppelt.");
            Assert.AreEqual(breite * hoehe, gespeichert.Select(c => (c.X, c.Y)).Distinct().Count(), "Eine Position steht doppelt.");
            Assert.AreEqual(world.Question.Id, gespeichert.Single(c => c.X == 0 && c.Y == 0).QuestionBaseId, "Die alte Zelle verlor ihre Frage.");
            CollectionAssert.AreEquivalent(gespeichert.Select(c => c.Id).ToList(), raster.CellVMs.Select(c => c.Coordinate.Id).ToList(),
                "Die Zellen im Raster sind nicht die gespeicherten.");
            await PruefeKoepfeAsync(spiel, raster, breite, hoehe);

            await GridBuilder.RebuildCells(spiel, CellView.Build);
            Assert.AreEqual(breite * hoehe, (await ZellenAsync(spiel.Id)).Count, "Der zweite Aufbau legte Zellen nach.");
            Assert.AreEqual(breite + hoehe, (await KoepfeAsync(spiel.Id)).Count, "Der zweite Aufbau legte Köpfe nach.");

            // P10-5 (P11): auch die nachgelegten Köpfe lassen nichts Ungespeichertes zurück - vorher 447 (20×20) bzw. 1687 (40×40).
            var offen = TestEnvironment.DiscardedChanges.ToList();
            Assert.AreEqual(0, offen.Count, "Ungespeichert: " + string.Join(" | ", offen));
        }

        /// <summary>P10-5: jeder Kopf einmal, mit seiner Nummer als Bezeichnung, und im Spiel stehen die gespeicherten.</summary>
        private static async Task PruefeKoepfeAsync(Game spiel, GridBuilder.GameGridVMs raster, int breite, int hoehe)
        {
            var koepfe = await KoepfeAsync(spiel.Id);

            Assert.AreEqual(breite + hoehe, koepfe.Count, "In der Datenbank fehlen Köpfe oder stehen doppelt.");
            Assert.AreEqual(breite + hoehe, koepfe.Select(k => (k.HeaderType, k.Index)).Distinct().Count(), "Ein Kopf steht doppelt.");
            Assert.IsTrue(koepfe.All(k => k.Designation == k.Index.ToString()), "Ein Kopf trägt nicht seine Nummer.");
            CollectionAssert.AreEquivalent(koepfe.Select(k => k.Id).ToList(), spiel.Headers.Select(k => k.Id).ToList(),
                "Die Köpfe im Spiel sind nicht die gespeicherten.");
            CollectionAssert.AreEqual(Enumerable.Range(1, breite).ToList(), raster.ColumnHeaderVMs.Select(k => k.Index).ToList(), "Die Spaltenköpfe stehen nicht in Folge.");
            CollectionAssert.AreEqual(Enumerable.Range(1, hoehe).ToList(), raster.RowHeaderVMs.Select(k => k.Index).ToList(), "Die Zeilenköpfe stehen nicht in Folge.");
        }

        private static async Task<List<GameGridCoordinate>> ZellenAsync(Guid spiel)
        {
            await using var db = new Quizzer.Logic.Context.DataContext();
            return await db.Set<GameGridCoordinate>().AsNoTracking().Where(c => c.GameId == spiel).ToListAsync();
        }

        private static async Task<List<Header>> KoepfeAsync(Guid spiel)
        {
            await using var db = new Quizzer.Logic.Context.DataContext();
            return await db.Set<Header>().AsNoTracking().Where(k => k.GameId == spiel).ToListAsync();
        }
    }
}
