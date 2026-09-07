using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using System.IO;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Der Programmstart sagt es, wenn der Datenordner fehlt.
    /// <para>
    /// <b>Gemessen 2026-09-07.</b> Für die Datenbank gab es diesen Weg längst — sie fragt nach,
    /// legt an und führt in die Einstellungen. Für den Datenordner gab es <b>gar nichts</b>:
    /// fehlt er, wirft nichts, denn <c>StaticResources</c> fällt für jedes nicht gefundene Bild
    /// auf Schwarz zurück. Wer das Programm gerade bekommen hat, startet es, sieht eine schwarze
    /// Oberfläche und bekommt <b>kein einziges Wort</b> dazu — obwohl nur ein Pfad falsch steht.
    /// </para>
    /// <para>
    /// Das trifft genau den Grund, aus dem die Einstellungen überhaupt entstanden sind:
    /// „damit ich das programm weitergeben kann ohne großen aufwand für den endnutzer"
    /// (2026-09-06).
    /// </para>
    /// </summary>
    [TestClass]
    public class DatenordnerBeimStartUnitTests
    {
        private string vorher = string.Empty;

        [TestInitialize]
        public void SetUp() => vorher = Settings.FilePathQuizzer;

        [TestCleanup]
        public void TearDown()
        {
            Settings.FilePathQuizzer = vorher;
            UserPrompt.Reset();
        }

        /// <summary>Ein fehlender Ordner wird gemeldet, mit Pfad und Folge.</summary>
        [TestMethod]
        public void AMissingDataFolderIsReported()
        {
            var fehlend = Path.Combine(Path.GetTempPath(), "quizzer-gibtesnicht-" + Guid.NewGuid());

            Assert.IsFalse(Directory.Exists(fehlend), "Die Probe braucht einen Ordner, den es NICHT gibt.");

            // false: die Einstellungen sollen NICHT aufgehen - ein modales Fenster hielte den
            // Testlauf an, statt ihn rot zu machen.
            var prompt = new RecordingUserPrompt(answer: false);

            UserPrompt.Current = prompt;

            Settings.FilePathQuizzer = fehlend;

            Quizzer.App.DatenordnerPruefen();

            Assert.AreEqual(1, prompt.Confirms.Count,
                "Der fehlende Datenordner wurde nicht gemeldet - das Programm startet mit "
                + "schwarzer Oberflaeche und sagt nicht warum.");

            StringAssert.Contains(prompt.Confirms[0].Message, fehlend,
                "Die Meldung nennt den Pfad nicht, um den es geht: " + prompt.Confirms[0].Message);

            StringAssert.Contains(prompt.Confirms[0].Message, "schwarz",
                "Die Meldung nennt die Folge nicht: " + prompt.Confirms[0].Message);

            Assert.IsFalse(Directory.Exists(fehlend),
                "Der Ordner wurde heimlich angelegt. Ein leerer Ordner behebt nichts - die "
                + "Bilder sind weiter weg -, aber der Hinweis kaeme beim naechsten Start nicht "
                + "mehr.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ist der Ordner da, kommt nichts — sonst stünde bei jedem
        /// Start eine Rückfrage im Weg.
        /// </summary>
        [TestMethod]
        public void AnExistingDataFolderSaysNothing()
        {
            var da = Path.Combine(Path.GetTempPath(), "quizzer-da-" + Guid.NewGuid());

            Directory.CreateDirectory(da);

            try
            {
                var prompt = new RecordingUserPrompt(answer: false);

                UserPrompt.Current = prompt;

                Settings.FilePathQuizzer = da;

                Quizzer.App.DatenordnerPruefen();

                Assert.AreEqual(0, prompt.Confirms.Count,
                    "Es wurde gefragt, obwohl der Ordner da ist: "
                    + string.Join(" | ", prompt.Confirms.Select(c => c.Message)));
            }
            finally
            {
                Directory.Delete(da, recursive: true);
            }
        }

        /// <summary>
        /// Ein <b>leerer</b> Eintrag ist derselbe Fall — und die Meldung sagt das, statt einen
        /// leeren Pfad zu zeigen.
        /// </summary>
        [TestMethod]
        public void AnEmptyDataFolderEntryIsAlsoReported()
        {
            var prompt = new RecordingUserPrompt(answer: false);

            UserPrompt.Current = prompt;

            Settings.FilePathQuizzer = string.Empty;

            Quizzer.App.DatenordnerPruefen();

            Assert.AreEqual(1, prompt.Confirms.Count,
                "Ein leerer Eintrag wurde nicht gemeldet.");

            StringAssert.Contains(prompt.Confirms[0].Message, "nicht eingetragen",
                "Die Meldung zeigt einen leeren Pfad statt zu sagen, dass keiner dasteht: "
                + prompt.Confirms[0].Message);
        }
    }
}
