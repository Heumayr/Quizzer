using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Views;
using System.IO;
using System.Windows;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Einstellungsmaske: Ordnerauswahl und die aus Einzelfeldern zusammengebaute Verbindung.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06 abends:</b> „die settings ... pfad muss auswählbar sein ...
    /// datenbank einstellungen aufdröseln und einzeln eingeben ... connectionstring wird
    /// zusammengebaut".
    /// </para>
    /// <para>
    /// <b>Die Ablage wird umgelenkt.</b> Ohne das schriebe jede dieser Zusicherungen in die
    /// echten Einstellungen dessen, der den Testlauf startet - und damit in dessen Verbindung zur
    /// echten Spieldatenbank.
    /// </para>
    /// </summary>
    [TestClass]
    public class SettingsViewModelUnitTests
    {
        private string ordner = string.Empty;
        private string datenordnerVorher = string.Empty;
        private string verbindungVorher = string.Empty;

        [TestInitialize]
        public void SetUp()
        {
            ordner = Path.Combine(Path.GetTempPath(), "quizzer-maske-" + Guid.NewGuid());

            UserSettings.UseFolderForTests(ordner);

            datenordnerVorher = Settings.FilePathQuizzer;
            verbindungVorher = Settings.ConnectionString;
        }

        [TestCleanup]
        public void TearDown()
        {
            UserSettings.UseFolderForTests(null);
            FilePicker.Reset();

            Settings.FilePathQuizzer = datenordnerVorher;
            Settings.ConnectionString = verbindungVorher;

            if (Directory.Exists(ordner))
                Directory.Delete(ordner, recursive: true);
        }

        private static SettingsViewModel Geladen()
        {
            var vm = new SettingsViewModel();

            vm.LoadForTestAsync().GetAwaiter().GetResult();

            return vm;
        }

        /// <summary>Der Ordner kommt aus dem Dialog, nicht aus der Tastatur.</summary>
        [TestMethod]
        public void TheFolderComesFromThePicker()
        {
            Settings.FilePathQuizzer = @"C:\Vorher";

            var vm = Geladen();

            Assert.AreEqual(@"C:\Vorher", vm.Datenordner);

            FilePicker.Current = new StummerWaehler(@"D:\Gewaehlt");

            vm.ChooseFolderCommand.Execute(null);

            Assert.AreEqual(@"D:\Gewaehlt", vm.Datenordner,
                "Der ausgewaehlte Ordner kommt nicht in der Maske an.");

            FilePicker.Current = new StummerWaehler(null);

            vm.ChooseFolderCommand.Execute(null);

            Assert.AreEqual(@"D:\Gewaehlt", vm.Datenordner,
                "Ein abgebrochener Dialog leert den Ordner - der Nutzer verliert seine Angabe.");
        }

        /// <summary>
        /// <b>Die tragende Zusicherung:</b> was in den Einzelfeldern steht, landet
        /// zusammengesetzt in der Einstellungsdatei - und was die Maske nicht anfasst, bleibt
        /// dabei stehen.
        /// </summary>
        [TestMethod]
        public void TheFieldsAreAssembledIntoTheSavedConnection()
        {
            Settings.FilePathQuizzer = ordner;
            Settings.ConnectionString =
                @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer;Integrated Security=True;"
                + "Connect Timeout=42";

            var vm = Geladen();

            Assert.AreEqual(@"(localdb)\MSSQLLocalDB", vm.Server);
            Assert.AreEqual("Quizzer", vm.Datenbank);
            Assert.IsTrue(vm.Windowsanmeldung);
            Assert.AreEqual(Visibility.Collapsed, vm.AnmeldedatenVisibility,
                "Bei Windows-Anmeldung haben Benutzer und Kennwort nichts in der Maske zu suchen.");

            vm.Server = @"SRV01\SQL";
            vm.Datenbank = "Quizabend";
            vm.Windowsanmeldung = false;
            vm.Benutzer = "quiz";
            vm.Kennwort = "geheim";

            Assert.AreEqual(Visibility.Visible, vm.AnmeldedatenVisibility);

            Assert.IsFalse(vm.Verbindungsvorschau.Contains("geheim", StringComparison.Ordinal),
                "Die Vorschau in der Maske zeigt das Kennwort im Klartext: "
                + vm.Verbindungsvorschau);

            vm.SaveCommand.Execute(null);

            var gespeichert = UserSettings.Load().MsSqlConString ?? string.Empty;

            StringAssert.Contains(gespeichert, @"Data Source=SRV01\SQL");
            StringAssert.Contains(gespeichert, "Initial Catalog=Quizabend");
            StringAssert.Contains(gespeichert, "User ID=quiz");
            StringAssert.Contains(gespeichert, "Password=geheim",
                "Ohne Kennwort kaeme die Verbindung nie zustande - gespeichert wird es "
                + "vollstaendig, nur angezeigt nicht.");

            StringAssert.Contains(gespeichert, "Connect Timeout=42",
                "Was die Maske nicht anfasst, wurde beim Zusammenbauen weggeworfen.");

            Assert.AreEqual(gespeichert, Settings.ConnectionString,
                "Das Gespeicherte gilt nicht sofort - erst der naechste Start wuerde es merken.");
        }

        /// <summary>Ein Dateiwähler, der immer dieselbe Antwort gibt.</summary>
        private sealed class StummerWaehler(string? antwort) : IFilePicker
        {
            public string? AskForSaveTarget(string titel, string vorschlag, string filter) => null;

            public string? AskForExistingFile(string titel, string filter) => null;

            public string? AskForFolder(string titel, string start) => antwort;
        }
    }
}
