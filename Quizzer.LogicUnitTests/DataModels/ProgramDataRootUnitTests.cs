using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Der Datenordner liegt per Vorgabe unter ProgramData, und was darunter liegt, gehört Quizzer.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-10-23 (M28):</b> „meine Ressourcen sollen auch auf die
    /// programdata zeigen ... kein separater Ort" - gewählt: ein Ordner
    /// <c>C:\ProgramData\Quizzer\Daten</c> für Einzel- und Servermodus. Vorher stand
    /// <c>E:\QuizzerData</c> in der ausgelieferten Datei, ein Pfad, der nur auf einem Rechner passte.
    /// </para>
    /// </summary>
    [TestClass]
    public class ProgramDataRootUnitTests
    {
        private string wurzelVorher = string.Empty;

        [TestInitialize]
        public void SetUp() => wurzelVorher = Settings.ProgramDataRoot;

        [TestCleanup]
        public void TearDown() => Settings.ProgramDataRoot = wurzelVorher;

        /// <summary>
        /// Ohne <c>appsettings.json</c> - so steht ein frischer Klon da, die Datei ist in der
        /// <c>.gitignore</c> - gelten die Vorgaben: der Datenordner unter ProgramData und die LocalDB.
        /// Vorher brach der Start ab.
        /// </summary>
        [TestMethod]
        public void WithoutAFileTheDefaultsApply()
        {
            var leer = Path.Combine(Path.GetTempPath(), "quizzer-ohne-appsettings-" + Guid.NewGuid());
            Directory.CreateDirectory(leer);
            var (ordnerVorher, verbindungVorher) = (Settings.FilePathQuizzer, Settings.ConnectionString);

            try
            {
                Settings.LoadSettingsFrom(leer);

                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                Assert.AreEqual(Path.Combine(programData, "Quizzer", "Daten"), Settings.FilePathQuizzer,
                    "Ohne Datei gilt ein anderer Datenordner - oder die Umgebungsvariable blieb unaufgelöst.");
                Assert.AreEqual(Settings.DefaultConnectionString, Settings.ConnectionString);
                Assert.IsTrue(Settings.IsUnderProgramDataRoot(Settings.FilePathQuizzer),
                    "Die Vorgabe liegt nicht unter ProgramDataRoot - Anlegen und Entfernen griffen nicht.");
            }
            finally
            {
                Settings.LoadSettings();
                (Settings.FilePathQuizzer, Settings.ConnectionString) = (ordnerVorher, verbindungVorher);
                Directory.Delete(leer, recursive: true);
            }
        }

        /// <summary>Was in der Datei steht, gilt weiter - auch mit Umgebungsvariable.</summary>
        [TestMethod]
        public void AFileStillOverridesTheDefaults()
        {
            var ordner = Path.Combine(Path.GetTempPath(), "quizzer-mit-appsettings-" + Guid.NewGuid());
            Directory.CreateDirectory(ordner);
            File.WriteAllText(Path.Combine(ordner, "appsettings.json"),
                """{ "AppSettings": { "FilePathQuizzer": "%TEMP%\\Eigen", "MsSqlConString": "Eigene-Verbindung" } }""");
            var (ordnerVorher, verbindungVorher) = (Settings.FilePathQuizzer, Settings.ConnectionString);

            try
            {
                Settings.LoadSettingsFrom(ordner);

                Assert.AreEqual(Path.Combine(Environment.GetEnvironmentVariable("TEMP")!, "Eigen"), Settings.FilePathQuizzer);
                Assert.AreEqual("Eigene-Verbindung", Settings.ConnectionString);
            }
            finally
            {
                Settings.LoadSettings();
                (Settings.FilePathQuizzer, Settings.ConnectionString) = (ordnerVorher, verbindungVorher);
                Directory.Delete(ordner, recursive: true);
            }
        }

        /// <summary>Darunter heißt darunter - ein Nachbar mit gleichem Anfang gehört nicht dazu.</summary>
        [TestMethod]
        public void OnlyFoldersBelowTheRootCount()
        {
            Settings.ProgramDataRoot = @"C:\PD\Quizzer";

            Assert.IsTrue(Settings.IsUnderProgramDataRoot(@"C:\PD\Quizzer\Daten"));
            Assert.IsTrue(Settings.IsUnderProgramDataRoot(@"c:\pd\quizzer\daten\Themes"), "Groß- und Kleinschreibung zählt unter Windows nicht.");
            Assert.IsFalse(Settings.IsUnderProgramDataRoot(@"C:\PD\QuizzerAlt\Daten"),
                "Ein Nachbarordner mit gleichem Anfang zählte mit - Entfernen träfe fremde Daten.");
            Assert.IsFalse(Settings.IsUnderProgramDataRoot(@"C:\PD\Quizzer\..\Fremd"), "Ein Pfad mit .. führt hinaus.");
            Assert.IsFalse(Settings.IsUnderProgramDataRoot(@"E:\QuizzerData"));
            Assert.IsFalse(Settings.IsUnderProgramDataRoot(null));
        }
    }
}
