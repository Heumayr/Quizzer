using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Einstellungen, die der Nutzer selbst setzt - Ablage, Überschreibung und Rückfall.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> „wo das programm die ressourcen ablegt muss auch
    /// im programm konfigurierbar sein ... eigentlich alle einstellungen", weil er das Programm
    /// weitergeben will. Die <c>appsettings.json</c> liegt im Programmordner, in den ein
    /// Endnutzer nicht schreiben darf.
    /// </para>
    /// <para>
    /// <b>Die Ablage wird für diesen Testlauf umgelenkt.</b> Ohne das schriebe jede dieser
    /// Zusicherungen in die echten Einstellungen dessen, der den Testlauf startet - und die
    /// letzte hätte sie gelöscht.
    /// </para>
    /// </summary>
    [TestClass]
    public class UserSettingsUnitTests
    {
        private string ordner = string.Empty;
        private string datenordnerVorher = string.Empty;
        private string verbindungVorher = string.Empty;

        [TestInitialize]
        public void SetUp()
        {
            ordner = Path.Combine(Path.GetTempPath(), "quizzer-einstellungen-" + Guid.NewGuid());

            UserSettings.UseFolderForTests(ordner);

            datenordnerVorher = Settings.FilePathQuizzer;
            verbindungVorher = Settings.ConnectionString;
        }

        [TestCleanup]
        public void TearDown()
        {
            UserSettings.UseFolderForTests(null);

            Settings.FilePathQuizzer = datenordnerVorher;
            Settings.ConnectionString = verbindungVorher;

            if (Directory.Exists(ordner))
                Directory.Delete(ordner, recursive: true);
        }

        /// <summary>Gespeichert, gelesen, angewandt - und die Datei liegt beim Nutzer.</summary>
        [TestMethod]
        public void SavedValuesOverrideTheShippedDefaults()
        {
            Settings.FilePathQuizzer = @"C:\Vorgabe";
            Settings.ConnectionString = "Vorgabe-Verbindung";

            Assert.IsFalse(UserSettings.Exists(), "Vor dem Speichern darf keine Datei da sein.");

            UserSettings.Save(@"C:\Eigener\Ordner", "Eigene-Verbindung");

            Assert.IsTrue(UserSettings.Exists(), "Es wurde keine Datei geschrieben.");
            Assert.IsTrue(UserSettings.FilePath.StartsWith(ordner, StringComparison.Ordinal),
                "Die Datei liegt nicht im umgelenkten Ordner: " + UserSettings.FilePath);

            Assert.AreEqual(@"C:\Eigener\Ordner", Settings.FilePathQuizzer,
                "Der eigene Datenordner wurde nicht angewandt.");
            Assert.AreEqual("Eigene-Verbindung", Settings.ConnectionString,
                "Die eigene Verbindung wurde nicht angewandt.");

            // Und nach einem Neustart: Vorgaben laden, dann die eigenen darueber.
            Settings.FilePathQuizzer = @"C:\Vorgabe";
            Settings.ConnectionString = "Vorgabe-Verbindung";

            UserSettings.Apply();

            Assert.AreEqual(@"C:\Eigener\Ordner", Settings.FilePathQuizzer);
            Assert.AreEqual("Eigene-Verbindung", Settings.ConnectionString);
        }

        /// <summary>
        /// Ein leeres Feld lässt die Vorgabe stehen - es setzt sie nicht auf leer.
        /// <para>
        /// Die Gegenrichtung zur Zusicherung oben: ohne sie wäre ungeprüft, ob „nichts
        /// eingetragen" von „ausdrücklich leer" unterschieden wird. Eine leer gesetzte
        /// Verbindungszeichenfolge macht das Programm unstartbar.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AnEmptyFieldLeavesTheDefaultAlone()
        {
            Settings.FilePathQuizzer = @"C:\Vorgabe";
            Settings.ConnectionString = "Vorgabe-Verbindung";

            UserSettings.Save(@"C:\Nur\Der\Ordner", string.Empty);

            Assert.AreEqual(@"C:\Nur\Der\Ordner", Settings.FilePathQuizzer);

            Assert.AreEqual("Vorgabe-Verbindung", Settings.ConnectionString,
                "Die leere Verbindung hat die Vorgabe ueberschrieben. Damit waere das Programm "
                + "nach dem Speichern nicht mehr startklar.");
        }

        /// <summary>
        /// Eine unlesbare Datei wirft nicht - sonst käme niemand mehr an die Maske heran, mit
        /// der er den Fehler beheben würde.
        /// </summary>
        [TestMethod]
        public void ABrokenFileIsIgnoredInsteadOfThrowing()
        {
            Directory.CreateDirectory(ordner);
            File.WriteAllText(UserSettings.FilePath, "{ das ist kein JSON");

            Settings.FilePathQuizzer = @"C:\Vorgabe";

            UserSettings.Apply();

            Assert.AreEqual(@"C:\Vorgabe", Settings.FilePathQuizzer,
                "Eine kaputte Datei hat die Vorgabe veraendert.");
        }

        /// <summary>
        /// <b>Die wichtigste Zusicherung dieser Klasse:</b> <c>LoadSettings</c> wendet die
        /// eigenen Werte <b>nicht</b> von selbst an.
        /// <para>
        /// Täte es das, griffe die Überschreibung auch dort, wo <c>DataContext</c> die
        /// Einstellungen lädt - und ein Testlauf schriebe in die <b>echte</b> Spieldatenbank
        /// dessen, der ihn gestartet hat. Genau dieselbe Verwechslung hat am 2026-09-06 schon
        /// einmal eine Messung verfälscht.
        /// </para>
        /// </summary>
        [TestMethod]
        public void LoadSettingsDoesNotApplyTheUsersValuesByItself()
        {
            UserSettings.Save(@"C:\Eigener\Ordner", "Eigene-Verbindung");

            Settings.LoadSettings();

            Assert.AreNotEqual("Eigene-Verbindung", Settings.ConnectionString,
                "LoadSettings hat die eigenen Werte angewandt. Dann greift die Ueberschreibung "
                + "auch im Testlauf und in jedem Werkzeug - und trifft die echte Spieldatenbank.");

            Assert.AreNotEqual(@"C:\Eigener\Ordner", Settings.FilePathQuizzer,
                "LoadSettings hat den eigenen Datenordner angewandt.");
        }
    }
}
