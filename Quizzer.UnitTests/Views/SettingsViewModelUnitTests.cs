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

            // Jede Zusicherung dieser Klasse speichert, und Speichern kann seit 2026-09-07 einen
            // Hinweis auslösen. Ohne Attrappe wäre das ein echtes modales Fenster im
            // Testprozess: der Lauf bleibt stehen, statt rot zu werden - gemessen, und es hat
            // eine Stunde gekostet. Wer eine Meldung PRÜFEN will, setzt sich seine eigene.
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
        }

        [TestCleanup]
        public void TearDown()
        {
            UserPrompt.Reset();
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

        /// <summary>
        /// <b>Ein leerer Datenbankname wird nicht gespeichert.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> das Feld liess sich leeren, und gespeichert wurde
        /// <c>Initial Catalog=</c>. Beim naechsten Start kam <b>keine</b> Rueckfrage - das
        /// Programm legte sein ganzes Schema in der Systemdatenbank <c>master</c> an. Der
        /// Pruefknopf sagte es schon laenger („Es ist aber keine Datenbank eingetragen"); das
        /// Speichern tat es nicht.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AnEmptyDatabaseNameIsRefused()
        {
            var prompt = new RecordingUserPrompt(answer: true);

            UserPrompt.Current = prompt;

            try
            {
                var vm = Geladen();

                vm.Datenordner = ordner;
                vm.Datenbank = string.Empty;

                vm.SaveCommand.Execute(null);

                Assert.IsFalse(vm.Saved,
                    "Der leere Datenbankname wurde gespeichert - das Programm legt seine "
                    + "Tabellen dann in der Systemdatenbank an.");

                Assert.AreEqual(1, prompt.Informs.Count, "Es kam keine Meldung.");

                StringAssert.Contains(prompt.Informs[0].Message, "Datenbank",
                    "Die Meldung sagt nicht, was fehlt: " + prompt.Informs[0].Message);
            }
            finally
            {
                UserPrompt.Reset();
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Vollständige Angaben werden gespeichert - sonst liesse sich
        /// die Maske gar nicht mehr benutzen.
        /// </summary>
        [TestMethod]
        public void CompleteSettingsAreSaved()
        {
            var prompt = new RecordingUserPrompt(answer: true);

            UserPrompt.Current = prompt;

            try
            {
                var vm = Geladen();

                vm.Datenordner = ordner;
                vm.Server = @"(localdb)\MSSQLLocalDB";
                vm.Datenbank = "Quizzer_Maskenprobe";

                vm.SaveCommand.Execute(null);

                Assert.IsTrue(vm.Saved,
                    "Vollstaendige Angaben liessen sich nicht speichern: "
                    + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
            }
            finally
            {
                UserPrompt.Reset();
            }
        }

        /// <summary>
        /// <b>Eine gewechselte Verbindung sagt es und nennt den Neustart.</b>
        /// <para>
        /// Sie wirkt sofort - jeder <c>DataContext</c> liest sie neu -, wurde aber weder geprüft
        /// noch migriert: das läuft nur beim Programmstart. Ohne Hinweis bekäme der Spielleiter
        /// beim nächsten Klick auf „Spiele" eine rohe <c>SqlException</c> oder
        /// „Invalid column name".
        /// </para>
        /// </summary>
        [TestMethod]
        public void AChangedConnectionAsksForARestart()
        {
            var prompt = new RecordingUserPrompt(answer: true);

            UserPrompt.Current = prompt;

            try
            {
                var vm = Geladen();

                vm.Datenordner = ordner;
                vm.Datenbank = "Quizzer_GanzAndere";

                vm.SaveCommand.Execute(null);

                Assert.IsTrue(vm.Saved, "Die Einstellungen liessen sich nicht speichern.");

                Assert.AreEqual(1, prompt.Informs.Count,
                    "Der Wechsel wurde nicht gemeldet - der naechste Klick auf 'Spiele' liefe "
                    + "in einen rohen Datenbankfehler.");

                StringAssert.Contains(prompt.Informs[0].Message, "neu starten",
                    "Die Meldung nennt den Weg nicht: " + prompt.Informs[0].Message);
            }
            finally
            {
                UserPrompt.Reset();
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Bleibt die Verbindung gleich, kommt keine Meldung - sonst
        /// erschiene sie bei jedem Speichern und würde weggeklickt.
        /// <para>
        /// <b>Diese Zusicherung war beim ersten Lauf rot, und der Befund lag im Programm.</b>
        /// Verglichen wurde die gespeicherte Zeichenfolge mit <c>Settings.ConnectionString</c> -
        /// dazwischen liegt aber der <c>SqlConnectionStringBuilder</c>, und der schreibt
        /// <c>Database=</c> zu <c>Initial Catalog=</c> um. Gemessen 2026-09-07 an genau der
        /// Zeichenfolge, die in der ausgelieferten <c>appsettings.json</c> steht: <b>der
        /// Neustart-Hinweis wäre bei jedem Speichern gekommen, auch beim allerersten und ohne
        /// jede Änderung</b>. Verglichen wird jetzt der Stand beim Laden, beide Seiten durch
        /// denselben Bauer.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AnUnchangedConnectionSaysNothing()
        {
            var prompt = new RecordingUserPrompt(answer: true);

            UserPrompt.Current = prompt;

            try
            {
                var vm = Geladen();

                vm.Datenordner = ordner;

                vm.SaveCommand.Execute(null);

                Assert.IsTrue(vm.Saved, "Die Einstellungen liessen sich nicht speichern.");

                Assert.AreEqual(0, prompt.Informs.Count,
                    "Es wurde ein Wechsel gemeldet, obwohl die Verbindung dieselbe ist: "
                    + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
            }
            finally
            {
                UserPrompt.Reset();
            }
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
