using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;
using Quizzer.Logic.Transfer;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Export und Import eines Spiels, von Ende zu Ende gegen die Datenbank.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06.</b> Der Import läuft mit dem angemeldeten Spielleiter, die
    /// Mitspieler werden dabei gewählt (oder keine), und Fragen werden <b>immer neu angelegt</b>
    /// (Entscheidung F09).
    /// </para>
    /// <para>
    /// Als Vorlage dient der Demo-Quizabend: er ist der einzige Bestand, der alle vier Fragetypen,
    /// Medien und ein volles Raster in einem Stück mitbringt.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameExportImportUnitTests
    {
        private string arbeitsordner = string.Empty;
        private string datenVorher = string.Empty;
        private readonly FileSystemMediaVault tresor = new();

        [TestInitialize]
        public void SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            datenVorher = Settings.FilePathQuizzer;

            arbeitsordner = Path.Combine(Path.GetTempPath(), "quizzer-buendel-" + Guid.NewGuid());
            Directory.CreateDirectory(arbeitsordner);

            Settings.FilePathQuizzer = arbeitsordner;
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await DemoDataRemover.RemoveAsync();

            await AufraeumenAsync();

            Settings.FilePathQuizzer = datenVorher;

            if (Directory.Exists(arbeitsordner))
                Directory.Delete(arbeitsordner, recursive: true);

            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>Räumt weg, was der Import angelegt hat - er trägt die Demo-Marke nicht.</summary>
        private static async Task AufraeumenAsync()
        {
            using var ctrlSpiele = new GamesController();

            foreach (var spiel in (await ctrlSpiele.GetAllAsync())
                     .Where(g => g.Designation.Contains("[Demo]", StringComparison.Ordinal)))
            {
                await ctrlSpiele.DeleteAsync(spiel.Id);
            }

            await ctrlSpiele.SaveChangesAsync();

            using var ctrlFragen = new QuestionBasesController();

            foreach (var frage in (await ctrlFragen.GetAllAsync())
                     .Where(f => f.Designation.Contains("[Demo]", StringComparison.Ordinal)))
            {
                await ctrlFragen.DeleteAsync(frage.Id);
            }

            await ctrlFragen.SaveChangesAsync();

            using var ctrlKategorien = new CategoriesController();

            foreach (var k in (await ctrlKategorien.GetAllAsync())
                     .Where(k => k.Designation.Contains("[Demo]", StringComparison.Ordinal)))
            {
                await ctrlKategorien.DeleteAsync(k.Id);
            }

            await ctrlKategorien.SaveChangesAsync();

            using var ctrlSpieler = new PlayersController();

            foreach (var p in (await ctrlSpieler.GetAllAsync())
                     .Where(p => p.Designation.Contains("[Demo]", StringComparison.Ordinal)))
            {
                await ctrlSpieler.DeleteAsync(p.Id);
            }

            await ctrlSpieler.SaveChangesAsync();
        }

        private async Task<(Guid SpielId, string Datei)> ExportiereDemoAsync()
        {
            var demo = await DemoDataSeeder.CreateAsync();

            var datei = Path.Combine(arbeitsordner, "probe" + GameExporter.Extension);

            await GameExporter.ExportAsync(demo.Spiel.Id, datei, tresor);

            return (demo.Spiel.Id, datei);
        }

        /// <summary>
        /// Der Durchstich: exportieren, importieren, und das Ergebnis steht wirklich in der
        /// Datenbank - mit Punkten, Schritten und dem angemeldeten Spielleiter.
        /// </summary>
        [TestMethod]
        public async Task AnExportedGameComesBackCompletely()
        {
            var (_, datei) = await ExportiereDemoAsync();

            Assert.IsTrue(File.Exists(datei), "Es wurde keine Datei geschrieben.");

            var moderator = Guid.NewGuid();

            using (var ctrlSpieler = new PlayersController())
            {
                await ctrlSpieler.InsertAsync(new Player
                {
                    Id = moderator,
                    Designation = "[Demo] Importeur",
                    DisplayName = "Importeur",
                });

                await ctrlSpieler.SaveChangesAsync();
            }

            var ergebnis = await GameImporter.ImportAsync(datei, tresor, moderator, [moderator]);

            Assert.AreEqual(12, ergebnis.Fragen, "Es kamen nicht alle zwoelf Fragen an.");

            using var ctrlSpiele = new GamesController();

            var importiert = await ctrlSpiele.GetAsync(ergebnis.Spiel.Id);

            Assert.IsNotNull(importiert, "Das importierte Spiel liess sich nicht laden.");

            Assert.AreEqual(moderator, importiert!.ModeratorPlayerId,
                "Der Spielleiter ist nicht der angemeldete - das Spiel liesse sich gar nicht starten.");

            Assert.AreEqual(1, importiert.PlayerXGames.Count,
                "Der gewaehlte Mitspieler wurde nicht eingetragen.");

            Assert.AreEqual(12, importiert.GameGridCoordinates.Count(c => c.QuestionBaseId.HasValue),
                "Nicht jede Zelle traegt wieder eine Frage.");

            var ohnePunkte = importiert.GameGridCoordinates
                .Where(c => c.QuestionBaseId.HasValue && c.CurrentPoints == 0)
                .ToList();

            Assert.AreEqual(0, ohnePunkte.Count,
                $"{ohnePunkte.Count} belegte Zellen haben null Punkte. Die Punkte sind gespeicherte "
                + "Spalten - ohne den Rechenaufruf nach dem Setzen von Spiel und Frage bleiben sie leer.");

            // Die Schritte, direkt gegen die Datenbank - nicht ueber ein ViewModel, das
            // Fehlendes stillschweigend nachlegt.
            using var ctrlFragen = new QuestionBasesController();

            foreach (var id in importiert.GameGridCoordinates
                         .Where(c => c.QuestionBaseId.HasValue)
                         .Select(c => c.QuestionBaseId!.Value))
            {
                var frage = await ctrlFragen.GetAsync(id);

                Assert.IsTrue(frage!.Steps.Count > 0,
                    $"Die Frage '{frage.Designation}' kam ohne Schritte an - das Spiel waere leer.");

                Assert.AreEqual(moderator, frage.OwnerPlayerId,
                    "Die importierte Frage gehoert nicht dem Importeur - sie waere fuer ihn unsichtbar.");
            }
        }

        /// <summary>
        /// Zweimal importieren legt <b>neben</b> den Bestand, nicht darüber - und die Kategorien
        /// wachsen dabei nicht mit.
        /// <para>
        /// <b>Nutzerentscheidung F09.</b> Eine doppelte Frage ist ein Klick; eine überschriebene
        /// wäre der Verlust eigener Arbeit, denn der Schreibweg löscht jeden Schritt, der nicht
        /// im Bündel steht.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task ImportingTwiceAddsInsteadOfOverwriting()
        {
            var (_, datei) = await ExportiereDemoAsync();

            var ersteZahlen = await ZaehleAsync();

            var erster = await GameImporter.ImportAsync(datei, tresor, null, []);
            var zweiter = await GameImporter.ImportAsync(datei, tresor, null, []);

            Assert.AreNotEqual(erster.Spiel.Id, zweiter.Spiel.Id,
                "Der zweite Import hat das Spiel des ersten ueberschrieben.");

            var danach = await ZaehleAsync();

            Assert.AreEqual(ersteZahlen.Spiele + 2, danach.Spiele, "Es fehlt ein Spiel.");
            Assert.AreEqual(ersteZahlen.Fragen + 24, danach.Fragen,
                "Die Fragen wurden nicht zweimal angelegt - dann hat der zweite Import den "
                + "ersten ueberschrieben, und dabei geht jeder Schritt verloren, der nicht im "
                + "Buendel steht.");

            Assert.AreEqual(ersteZahlen.Kategorien, danach.Kategorien,
                "Die Kategorien haben sich vermehrt. Sie sollen zusammengefuehrt werden - sie "
                + "tragen ausser ihrer Bezeichnung nichts.");

            Assert.AreEqual(0, zweiter.NeueKategorien,
                "Der zweite Import meldet neue Kategorien, obwohl es dieselben sind.");
        }

        private static async Task<(int Spiele, int Fragen, int Kategorien)> ZaehleAsync()
        {
            using var ctrlSpiele = new GamesController();
            using var ctrlFragen = new QuestionBasesController();
            using var ctrlKategorien = new CategoriesController();

            return (
                (await ctrlSpiele.GetAllAsync()).Length,
                (await ctrlFragen.GetAllAsync()).Length,
                (await ctrlKategorien.GetAllAsync()).Length);
        }

        /// <summary>
        /// Ein Bündel, das gar keines ist, wird abgewiesen - mit einem Satz, der sagt warum.
        /// </summary>
        [TestMethod]
        public async Task AFileThatIsNoBundleIsRejected()
        {
            var datei = Path.Combine(arbeitsordner, "kein-buendel" + GameExporter.Extension);

            await File.WriteAllTextAsync(datei, "das ist einfach nur Text");

            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => GameImporter.ImportAsync(datei, tresor, null, []),
                "Eine Datei, die kein Buendel ist, wurde angenommen.");
        }
    }
}
