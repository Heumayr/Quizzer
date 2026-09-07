using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views;
using System.IO;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Eine Datei mit der Endung eines Bildes, deren Inhalt keines ist.
    /// <para>
    /// <b>Gemessen 2026-09-07.</b> Die Bildwähler prüfen seit gestern die <i>Endung</i> an der
    /// Quelle — richtig, aber nicht genug: eine <c>.jpg</c>, die SkiaSharp nicht dekodieren kann
    /// (ein CMYK-JPEG, eine umbenannte Datei), kam durch die Prüfung und ließ
    /// <c>HandleSelectedResourceFile</c> werfen. Beim Mitspielerbild und im Aufdeck-Editor gab
    /// es dafür <b>keinen Fänger</b> — es stand ein Fehlerfenster mit Stapelspur da, mitten in
    /// der Vorbereitung.
    /// </para>
    /// <para>
    /// Der geworfene Text ist deshalb ein deutscher Satz und kein <c>"Image could not be
    /// loaded."</c> — er landet vor dem Nutzer.
    /// </para>
    /// </summary>
    [TestClass]
    public class UnlesbaresBildWaehlenUnitTests
    {
        private string ordner = string.Empty;
        private string kaputtesBild = string.Empty;
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            ordner = Path.Combine(Path.GetTempPath(), "quizzer-bildprobe-" + Guid.NewGuid());

            Directory.CreateDirectory(ordner);

            kaputtesBild = Path.Combine(ordner, "kein-bild.jpg");

            File.WriteAllText(kaputtesBild, "Das ist kein Bild, sondern Text.");

            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void TearDown()
        {
            UserPrompt.Reset();
            FilePicker.Reset();

            if (Directory.Exists(ordner))
                Directory.Delete(ordner, recursive: true);
        }

        /// <summary>
        /// <b>Der Kern:</b> die Ausnahme trägt einen Satz, den der Spielleiter lesen kann — und
        /// den Dateinamen.
        /// </summary>
        [TestMethod]
        public void TheThrownTextIsAGermanSentenceNamingTheFile()
        {
            var ziel = Path.Combine(ordner, "ziel.png");

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => FileHelper.SaveAsPng(kaputtesBild, ziel),
                "Eine Textdatei mit der Endung .jpg wurde anstandslos umgewandelt - dann misst "
                + "der Rest nichts.");

            StringAssert.Contains(ex.Message, "kein-bild.jpg",
                "Die Meldung nennt die Datei nicht: " + ex.Message);

            Assert.IsFalse(ex.Message.Contains("could not be loaded", StringComparison.OrdinalIgnoreCase),
                "Die Meldung ist noch englisch - sie landet aber ueber die Faenger vor dem "
                + "Nutzer: " + ex.Message);

            StringAssert.Contains(ex.Message, "lässt sich nicht lesen",
                "Die Meldung sagt nicht, was los ist: " + ex.Message);
        }

        /// <summary>
        /// <b>Der Bildwähler des Mitspielers meldet statt zu werfen.</b>
        /// </summary>
        [TestMethod]
        public void ThePlayerPictureIsReportedInsteadOfThrown()
        {
            var vorher = Settings.FilePathQuizzer;

            Settings.FilePathQuizzer = ordner;

            try
            {
                var vm = new EditPlayerViewModel();

                vm.SetPlayer(new Player { Id = Guid.NewGuid(), Designation = "Probe" });

                FilePicker.Current = new StummerDateiwaehler(kaputtesBild);

                TestEnvironment.RunCommandAsync(vm.SelectResourceCommnad).GetAwaiter().GetResult();

                Assert.AreEqual(1, prompt.Informs.Count,
                    "Es kam keine Meldung - dann ist die Ausnahme durchgeschlagen und der "
                    + "Spielleiter sieht ein Fehlerfenster mit Stapelspur.");

                StringAssert.Contains(prompt.Informs[0].Message, "kein-bild.jpg",
                    "Die Meldung nennt die Datei nicht: " + prompt.Informs[0].Message);

                TestEnvironment.ThrowIfAnythingWasSwallowed();
            }
            finally
            {
                Settings.FilePathQuizzer = vorher;
            }
        }

        /// <summary>Ein Dateiwähler, der immer dieselbe Datei liefert.</summary>
        private sealed class StummerDateiwaehler(string? antwort) : IFilePicker
        {
            public string? AskForSaveTarget(string titel, string vorschlag, string filter) => null;

            public string? AskForExistingFile(string titel, string filter) => antwort;

            public string? AskForFolder(string titel, string start) => null;
        }
    }
}
