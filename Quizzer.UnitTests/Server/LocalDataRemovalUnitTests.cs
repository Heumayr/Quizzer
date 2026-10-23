using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Server;
using System.IO;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Was „Daten von diesem Rechner entfernen" löscht - und was nicht.
    /// <para>
    /// <b>Nutzerwort vom 2026-10-23 (M28):</b> „mann soll aber seine daten mit einem mal aus den
    /// programdata entfernen können". Der Weg löscht Ordner und Datenbanken; jede Zusicherung hier
    /// prüft deshalb auch die Gegenrichtung: was daneben liegt, bleibt.
    /// </para>
    /// <para>
    /// <b>Alle Ziele sind umgelenkt</b> - die echten wären die Daten dessen, der den Testlauf startet.
    /// </para>
    /// </summary>
    [TestClass]
    public class LocalDataRemovalUnitTests
    {
        private const string Master = @"Data Source=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True";

        private string basis = string.Empty;

        [TestInitialize]
        public void SetUp()
        {
            basis = Path.Combine(Path.GetTempPath(), "quizzer-entfernen-" + Guid.NewGuid());
            Directory.CreateDirectory(basis);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(basis))
                Directory.Delete(basis, recursive: true);
        }

        /// <summary>Nur die Form der Zwischendatenbank - genau die, die ServerSession erzeugt.</summary>
        [TestMethod]
        public void OnlyCacheDatabaseNamesQualify()
        {
            Assert.IsTrue(LocalDataRemoval.IsCacheDatabaseName(ServerSession.CacheDatabaseName(Guid.NewGuid())),
                "Der Riegel ließe die eigenen Zwischendatenbanken stehen.");

            foreach (var name in new[] { "Quizzer", "Quizzer_App_Tests", "Quizzer_Server_Tests", "master",
                         "Quizzer_Server_EA94687078FE", "Quizzer_Server_ea94687078fe0", "Quizzer_Server_ea94687078fe]; DROP DATABASE [Quizzer" })
            {
                Assert.IsFalse(LocalDataRemoval.IsCacheDatabaseName(name), "Fiele mit: " + name);
            }
        }

        /// <summary>Unter der Wurzel geht alles, daneben nichts; die Wurzel selbst bleibt.</summary>
        [TestMethod]
        public async Task EverythingBelowTheRootGoesNothingBeside()
        {
            Anlegen(@"Quizzer\Daten\Resources\bild.png", @"Quizzer\Daten\Background.png", @"Quizzer\Server\x\y.png", @"Quizzer\lose.txt",
                @"zustand\server\session-1.bin", @"zustand\server\sync-1.json", @"QuizzerAlt\bleibt.txt", @"zustand\bleibt-auch.txt");
            var (wurzel, zustand) = (Ordner("Quizzer"), Ordner(@"zustand\server"));

            var fehler = await LocalDataRemoval.RemoveAsync(Ziele(wurzel, zustand, _ => false));

            Assert.AreEqual(0, fehler.Count, string.Join(" | ", fehler));
            Assert.IsTrue(Directory.Exists(wurzel), "Die Wurzel selbst sollte bleiben.");
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(wurzel).Count(), "Unter der Wurzel blieb etwas.");
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(zustand).Count(), "Die gemerkten Anmeldungen blieben.");
            Assert.IsTrue(File.Exists(Path.Combine(basis, @"QuizzerAlt\bleibt.txt")), "Ein Nachbarordner wurde geleert.");
            Assert.IsTrue(File.Exists(Path.Combine(basis, @"zustand\bleibt-auch.txt")), "Der Ordner über den Anmeldungen wurde geleert.");
        }

        /// <summary>Eine Wurzel mit anderem Namen wird nicht angefasst - ein verbogener Pfad leert nicht den Ordner darüber.</summary>
        [TestMethod]
        public async Task ARootWithAnotherNameIsLeftAlone()
        {
            Anlegen(@"Fremd\wichtig.txt", @"zustand\server\session-1.bin");
            var (fremd, zustand) = (Ordner("Fremd"), Ordner(@"zustand\server"));

            var fehler = await LocalDataRemoval.RemoveAsync(Ziele(fremd, zustand, _ => false));

            Assert.IsTrue(File.Exists(Path.Combine(fremd, "wichtig.txt")), "Ein Ordner, der nicht „Quizzer“ heißt, wurde geleert.");
            Assert.IsTrue(fehler.Any(f => f.Contains("nicht angefasst", StringComparison.Ordinal)),
                "Dass nichts angefasst wurde, steht nicht in den Fehlern: " + string.Join(" | ", fehler));
        }

        /// <summary>
        /// Die gewählte Zwischendatenbank fällt, auch während sie benutzt wird - eine andere daneben
        /// bleibt. Gegen die echte LocalDB, mit Wegwerfnamen.
        /// </summary>
        [TestMethod]
        public async Task ExactlyTheSelectedDatabaseIsDroppedEvenWhileInUse()
        {
            var ziel = "Quizzer_Server_" + Guid.NewGuid().ToString("N")[..12];
            var daneben = "Quizzer_M28Daneben_" + Guid.NewGuid().ToString("N")[..8] + "_Tests";
            await Ausfuehren($"CREATE DATABASE [{ziel}]; CREATE DATABASE [{daneben}];");

            try
            {
                await using var offen = new SqlConnection(Master.Replace("master", ziel));
                await offen.OpenAsync();

                Anlegen(@"Quizzer\a.txt", @"zustand\server\b.txt");
                var fehler = await LocalDataRemoval.RemoveAsync(
                    Ziele(Ordner("Quizzer"), Ordner(@"zustand\server"), n => n == ziel && LocalDataRemoval.IsCacheDatabaseName(n)));

                Assert.AreEqual(0, fehler.Count, string.Join(" | ", fehler));
                Assert.IsFalse(await Gibt(ziel), "Die Zwischendatenbank steht noch.");
                Assert.IsTrue(await Gibt(daneben), "Eine nicht gewählte Datenbank wurde gelöscht.");
            }
            finally
            {
                SqlConnection.ClearAllPools();
                await Ausfuehren($"IF DB_ID('{ziel}') IS NOT NULL BEGIN ALTER DATABASE [{ziel}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{ziel}]; END; "
                    + $"IF DB_ID('{daneben}') IS NOT NULL DROP DATABASE [{daneben}];");
            }
        }

        private static LocalDataRemoval.Targets Ziele(string wurzel, string zustand, Func<string, bool> wahl)
            => new(wurzel, zustand, Master, wahl);

        private string Ordner(string relativ) => Path.Combine(basis, relativ);

        /// <summary>Legt Dateien unter der Probe an.</summary>
        private void Anlegen(params string[] dateien)
        {
            foreach (var datei in dateien)
            {
                var voll = Path.Combine(basis, datei);
                Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
                File.WriteAllText(voll, "x");
            }
        }

        private static async Task Ausfuehren(string sql)
        {
            await using var verbindung = new SqlConnection(Master);
            await verbindung.OpenAsync();
            await using var befehl = new SqlCommand(sql, verbindung);
            await befehl.ExecuteNonQueryAsync();
        }

        private static async Task<bool> Gibt(string name)
        {
            await using var verbindung = new SqlConnection(Master);
            await verbindung.OpenAsync();
            await using var befehl = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @n", verbindung);
            befehl.Parameters.AddWithValue("@n", name);
            return (int)(await befehl.ExecuteScalarAsync())! > 0;
        }
    }
}
