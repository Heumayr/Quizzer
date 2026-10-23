using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server;
using Quizzer.Views;
using System.IO;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Im Servermodus schaltet das Programm seine Medien auf einen Ordner unter ProgramData um -
    /// so wie es die Datenbank auf die Zwischendatenbank des Kontos umschaltet.
    /// <para>
    /// <b>Nutzerwort vom 2026-10-23 (M26):</b> „die datein die geladen werden von der api ... in
    /// den programdata folder landen per default". Vorher legte der Abgleich sie in den
    /// Datenordner des Einzelbetriebs.
    /// </para>
    /// <para>
    /// <b>Die zweite Hälfte ist die gefährlichere:</b> nach dem Speichern der Einstellungen
    /// setzt <c>UserSettings.Save</c> den eigenen Ordner wieder ein. Ohne Rücksprung arbeitete die
    /// Sitzung danach gegen den falschen Ordner, und die Maske schriebe den ProgramData-Ordner als
    /// eigenen Datenordner fest.
    /// </para>
    /// </summary>
    [TestClass]
    public class ServerSessionMediaUnitTests
    {
        private static readonly Uri Server = new("https://localhost:8543/api/v1/");

        private string ordner = string.Empty;
        private string programData = string.Empty;
        private string datenordnerVorher = string.Empty;
        private string verbindungVorher = string.Empty;

        [TestInitialize]
        public void SetUp()
        {
            ordner = Path.Combine(Path.GetTempPath(), "quizzer-servermedien-" + Guid.NewGuid());
            programData = Path.Combine(ordner, "ProgramData");

            UserSettings.UseFolderForTests(Path.Combine(ordner, "Einstellungen"));
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            datenordnerVorher = Settings.FilePathQuizzer;
            verbindungVorher = Settings.ConnectionString;
            Settings.FilePathQuizzer = @"E:\Lokal";
        }

        [TestCleanup]
        public void TearDown()
        {
            ServerSession.ForgetServerMediaForTests();
            UserPrompt.Reset();
            UserSettings.UseFolderForTests(null);

            Settings.FilePathQuizzer = datenordnerVorher;
            Settings.ConnectionString = verbindungVorher;

            if (Directory.Exists(ordner))
                Directory.Delete(ordner, recursive: true);
        }

        /// <summary>Angemeldet: Medien unter ProgramData, der eigene Ordner bleibt gemerkt.</summary>
        [TestMethod]
        public void SigningInSwitchesTheMediaToTheServersFolder()
        {
            ServerSession.UseServerMedia(Server, programData);

            var erwartet = ServerMediaFolder.For(Server, programData);
            Assert.AreEqual(erwartet, Settings.FilePathQuizzer, "Die Medien zeigen nicht auf den Ordner des Servers.");
            Assert.AreEqual(erwartet, ServerSession.CacheMediaFolder, "Der Abgleich bekäme einen anderen Ordner als das Programm.");
            Assert.AreEqual(@"E:\Lokal", ServerSession.LocalFilePath, "Der eigene Datenordner ging verloren.");
            Assert.IsTrue(Directory.Exists(erwartet), "Der Ordner wurde nicht angelegt.");
        }

        /// <summary>
        /// Ein zweites Umschalten in derselben Sitzung (etwa nach dem Offline-Start) merkt sich
        /// nicht den ProgramData-Ordner als eigenen.
        /// </summary>
        [TestMethod]
        public void SwitchingTwiceKeepsTheOwnFolder()
        {
            ServerSession.UseServerMedia(Server, programData);
            ServerSession.UseServerMedia(new Uri("https://quiz-pc.tail1.ts.net:8543/"), programData);

            Assert.AreEqual(@"E:\Lokal", ServerSession.LocalFilePath);
        }

        /// <summary>Nach dem Speichern der Einstellungen bleibt die Sitzung beim Ordner des Servers.</summary>
        [TestMethod]
        public void SavingTheSettingsKeepsTheServersFolder()
        {
            ServerSession.UseServerMedia(Server, programData);

            // Was die Maske beim Speichern tut: eigene Werte schreiben (setzt Settings.FilePathQuizzer
            // zurueck auf den eigenen Ordner), dann zurueckspringen.
            UserSettings.Save(@"F:\Neu", verbindungVorher);
            Assert.AreEqual(@"F:\Neu", Settings.FilePathQuizzer, "Vorbedingung: Save setzt den eigenen Ordner ein.");

            ServerSession.KeepSessionDatabase();

            Assert.AreEqual(ServerMediaFolder.For(Server, programData), Settings.FilePathQuizzer,
                "Nach dem Speichern arbeitete die Sitzung gegen den eigenen Ordner statt gegen den des Servers.");
            Assert.AreEqual(@"F:\Neu", ServerSession.LocalFilePath, "Der neu gespeicherte eigene Ordner wurde nicht übernommen.");
        }

        /// <summary>Ohne Server ändert der Rücksprung nichts - der Einzelbetrieb bleibt beim eigenen Ordner.</summary>
        [TestMethod]
        public void WithoutServerTheDataFolderStays()
        {
            ServerSession.KeepSessionDatabase();

            Assert.AreEqual(@"E:\Lokal", Settings.FilePathQuizzer);
            Assert.IsNull(ServerSession.LocalFilePath);
        }

        /// <summary>Die Einstellungsmaske zeigt im Servermodus den eigenen Ordner, nicht den unter ProgramData.</summary>
        [TestMethod]
        public void TheSettingsMaskShowsTheOwnFolder()
        {
            ServerSession.UseServerMedia(Server, programData);

            var vm = new SettingsViewModel();
            vm.LoadForTestAsync().GetAwaiter().GetResult();

            Assert.AreEqual(@"E:\Lokal", vm.Datenordner,
                "Die Maske zeigte den Medienordner des Servers - gespeichert würde er zum eigenen Datenordner.");
        }
    }
}
