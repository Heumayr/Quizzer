using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Server;
using Quizzer.Views;
using System.IO;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// „Daten von diesem Rechner entfernen" in der Einstellungsmaske: Rückfrage, Löschen, Neustart.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-10-23 (M28):</b> der Knopf steht immer da, auch ohne Server -
    /// dort ist es die einzige Kopie, und die Rückfrage muss das sagen.
    /// </para>
    /// </summary>
    [TestClass]
    public class DatenEntfernenUnitTests
    {
        private string basis = string.Empty;
        private string wurzelVorher = string.Empty;
        private string datenordnerVorher = string.Empty;
        private int neustarts;

        [TestInitialize]
        public void SetUp()
        {
            basis = Path.Combine(Path.GetTempPath(), "quizzer-maske-entfernen-" + Guid.NewGuid());
            wurzelVorher = Settings.ProgramDataRoot;
            datenordnerVorher = Settings.FilePathQuizzer;

            Settings.ProgramDataRoot = Path.Combine(basis, "Quizzer");
            Settings.FilePathQuizzer = Path.Combine(Settings.ProgramDataRoot, "Daten");
            Directory.CreateDirectory(Path.Combine(Settings.FilePathQuizzer, "Resources"));
            File.WriteAllText(Path.Combine(Settings.FilePathQuizzer, "Resources", "bild.png"), "x");

            var zustand = Path.Combine(basis, "zustand", "server");
            Directory.CreateDirectory(zustand);
            LocalDataRemoval.TargetsForTests = () => new LocalDataRemoval.Targets(
                Settings.ProgramDataRoot, zustand, @"Data Source=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True", _ => false);

            UserSettings.UseFolderForTests(Path.Combine(basis, "einstellungen"));
            neustarts = 0;
            AppRestart.Current = () => neustarts++;
        }

        [TestCleanup]
        public void TearDown()
        {
            LocalDataRemoval.TargetsForTests = null;
            AppRestart.Reset();
            UserPrompt.Reset();
            UserSettings.UseFolderForTests(null);
            Settings.ProgramDataRoot = wurzelVorher;
            Settings.FilePathQuizzer = datenordnerVorher;

            if (Directory.Exists(basis))
                Directory.Delete(basis, recursive: true);
        }

        /// <summary>„Nein" heißt: nichts gelöscht, kein Neustart.</summary>
        [TestMethod]
        public async Task DecliningRemovesNothing()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: false);

            await ((AsyncRelayCommand)new SettingsViewModel().RemoveDataCommand).ExecuteAsync(null);

            Assert.IsTrue(File.Exists(Path.Combine(Settings.FilePathQuizzer, "Resources", "bild.png")), "Trotz „Nein“ gelöscht.");
            Assert.AreEqual(0, neustarts, "Trotz „Nein“ neu gestartet.");
        }

        /// <summary>
        /// Bestätigt: alles unter ProgramData weg, dann Neustart. Die Rückfrage nennt den Ort und
        /// sagt ohne Server, dass es die einzige Kopie ist.
        /// </summary>
        [TestMethod]
        public async Task ConfirmingRemovesEverythingAndRestarts()
        {
            var prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            await ((AsyncRelayCommand)new SettingsViewModel().RemoveDataCommand).ExecuteAsync(null);

            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(Settings.ProgramDataRoot).Count(), "Unter ProgramData blieb etwas.");
            Assert.AreEqual(1, neustarts, "Nach dem Entfernen startete das Programm nicht neu.");
            Assert.AreEqual(0, prompt.Informs.Count, "Eine Fehlermeldung, obwohl alles ging: " + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            var frage = prompt.Confirms.Single().Message;
            StringAssert.Contains(frage, Settings.ProgramDataRoot, "Die Rückfrage nennt den Ort nicht.");
            StringAssert.Contains(frage, "einzige Kopie", "Ohne Server sagt die Rückfrage nicht, dass es die einzige Kopie ist: " + frage);
            Assert.IsFalse(frage.Contains("bleibt unberührt", StringComparison.Ordinal),
                "Der Datenordner liegt unter ProgramData - „bleibt unberührt“ wäre gelogen.");
        }

        /// <summary>Ein Datenordner außerhalb von ProgramData wird nicht gelöscht - und die Rückfrage sagt das.</summary>
        [TestMethod]
        public async Task AnOwnFolderElsewhereIsNamedAsUntouched()
        {
            var eigener = Path.Combine(basis, "Eigener");
            Directory.CreateDirectory(eigener);
            File.WriteAllText(Path.Combine(eigener, "bleibt.png"), "x");
            Settings.FilePathQuizzer = eigener;
            var prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            await ((AsyncRelayCommand)new SettingsViewModel().RemoveDataCommand).ExecuteAsync(null);

            StringAssert.Contains(prompt.Confirms.Single().Message, eigener + " liegt nicht darunter und bleibt unberührt");
            Assert.IsTrue(File.Exists(Path.Combine(eigener, "bleibt.png")), "Der eigene Ordner außerhalb wurde geleert.");
        }
    }
}
