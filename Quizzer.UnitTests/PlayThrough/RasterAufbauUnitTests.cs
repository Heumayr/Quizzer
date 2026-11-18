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

            await GridBuilder.RebuildCells(spiel, CellView.Build);
            Assert.AreEqual(breite * hoehe, (await ZellenAsync(spiel.Id)).Count, "Der zweite Aufbau legte Zellen nach.");
            // Nur die Zellen: die nachgelegten Köpfe hinterlassen im HeadersController schon vorher offene Änderungen (Bestand, P10 gemeldet).
            var offen = TestEnvironment.DiscardedChanges.Where(d => d.StartsWith(nameof(GameGridCoordinatesController))).ToList();
            Assert.AreEqual(0, offen.Count, "Ungespeichert: " + string.Join(" | ", offen));
        }

        private static async Task<List<GameGridCoordinate>> ZellenAsync(Guid spiel)
        {
            await using var db = new Quizzer.Logic.Context.DataContext();
            return await db.Set<GameGridCoordinate>().AsNoTracking().Where(c => c.GameId == spiel).ToListAsync();
        }
    }
}
