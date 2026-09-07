using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Themes;
using Quizzer.Views;
using Quizzer.Views.StaticRessources;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Eine Textur für ein Design austauschen.
    /// <para>
    /// <b>Zwei Befunde, beide 2026-09-07 gemessen.</b> Der Wähler bot „Alle Dateien" an und
    /// kopierte, was er bekam, unter dem <i>festen</i> Texturnamen in den Design-Ordner. Eine
    /// Nicht-Bilddatei wurde damit zu einer „CellBackground.png", die sich nicht laden lässt —
    /// und weil eine fehlende Textur still auf den Auslieferungsstand zurückfällt, änderte sich
    /// <b>gar nichts</b>, ohne ein Wort. Man hätte es für einen wirkungslosen Klick gehalten.
    /// </para>
    /// <para>
    /// Und er machte seinen Dateidialog selbst auf, an <c>FilePicker</c> vorbei — die zweite von
    /// zwei solchen Stellen. Genau deshalb gab es hier bis heute keine einzige Zusicherung: sie
    /// hätte im Testlauf ein echtes Dateifenster geöffnet und den Lauf angehalten.
    /// </para>
    /// </summary>
    [TestClass]
    public class TexturWaehlenUnitTests
    {
        private string wurzel = null!;
        private string vorherPfad = null!;
        private RecordingUserPrompt prompt = null!;
        private GameTheme design = null!;

        [TestInitialize]
        public void SetUp()
        {
            vorherPfad = Quizzer.DataModels.Settings.FilePathQuizzer;

            wurzel = Path.Combine(Path.GetTempPath(), "QuizzerTextur-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(wurzel, "Themes", "Probe"));

            Quizzer.DataModels.Settings.FilePathQuizzer = wurzel;

            ThemeBrushes.Forget();

            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            design = new GameTheme
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                FolderName = "Probe",
            };
        }

        [TestCleanup]
        public void TearDown()
        {
            Quizzer.DataModels.Settings.FilePathQuizzer = vorherPfad;

            ThemeBrushes.Forget();
            UserPrompt.Reset();
            FilePicker.Reset();

            try
            {
                if (Directory.Exists(wurzel))
                    Directory.Delete(wurzel, recursive: true);
            }
            catch
            {
                // Ein liegengebliebener Temp-Ordner faerbt keinen Test rot.
            }
        }

        /// <summary>Schreibt ein winziges, gültiges PNG.</summary>
        private static void SchreibPng(string pfad)
        {
            var bild = BitmapSource.Create(
                2, 2, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
                new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 }, 8);

            var kodierer = new PngBitmapEncoder();

            kodierer.Frames.Add(BitmapFrame.Create(bild));

            using var strom = File.Create(pfad);

            kodierer.Save(strom);
        }

        private (GameThemesViewModel Vm, ThemeTextureItem Item) Aufbau()
        {
            var vm = new GameThemesViewModel { SelectedTheme = design };

            var item = vm.Textures.First();

            return (vm, item);
        }

        private string Zielpfad(ThemeTextureItem item)
            => Path.Combine(wurzel, "Themes", "Probe", item.Dateiname);

        /// <summary>
        /// <b>Eine Datei, die kein Bild ist, wird nicht übernommen — und das wird gesagt.</b>
        /// </summary>
        [TestMethod]
        public void ANonImageIsRefusedAndSaidSo()
        {
            var (vm, item) = Aufbau();

            var kaputt = Path.Combine(wurzel, "kein-bild.png");

            File.WriteAllText(kaputt, "Das ist kein Bild.");

            FilePicker.Current = new StummerWaehler(kaputt);

            ((ICommand)vm.ChooseTextureCommand).Execute(item);

            Assert.IsFalse(File.Exists(Zielpfad(item)),
                "Die Nicht-Bilddatei liegt jetzt als Textur im Design-Ordner. Sie laesst sich "
                + "nicht laden, das Design faellt still auf den Auslieferungsstand zurueck - und "
                + "der Klick sieht wirkungslos aus.");

            Assert.AreEqual(1, prompt.Informs.Count,
                "Es kam keine Meldung: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            StringAssert.Contains(prompt.Informs[0].Message, "kein-bild.png",
                "Die Meldung nennt die Datei nicht: " + prompt.Informs[0].Message);
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ein echtes Bild wird übernommen und nicht kommentiert —
        /// sonst wäre die Prüfung oben auch grün, wenn sie <i>alles</i> abwiese.
        /// </summary>
        [TestMethod]
        public void ARealImageIsTakenWithoutAWord()
        {
            var (vm, item) = Aufbau();

            var gut = Path.Combine(wurzel, "echtes-bild.png");

            SchreibPng(gut);

            FilePicker.Current = new StummerWaehler(gut);

            ((ICommand)vm.ChooseTextureCommand).Execute(item);

            Assert.IsTrue(File.Exists(Zielpfad(item)),
                "Ein gueltiges PNG wurde nicht uebernommen - dann ist der Weg zu.");

            Assert.AreEqual(0, prompt.Informs.Count,
                "Ein gueltiges Bild wurde beanstandet: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>
        /// Ein abgebrochener Dialog ändert nichts und sagt nichts.
        /// </summary>
        [TestMethod]
        public void ACancelledDialogChangesNothing()
        {
            var (vm, item) = Aufbau();

            FilePicker.Current = new StummerWaehler(null);

            ((ICommand)vm.ChooseTextureCommand).Execute(item);

            Assert.IsFalse(File.Exists(Zielpfad(item)), "Nach einem Abbruch liegt eine Textur da.");

            Assert.AreEqual(0, prompt.Informs.Count,
                "Ein Abbruch wurde kommentiert: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>Ein Dateiwähler, der immer dieselbe Antwort gibt.</summary>
        private sealed class StummerWaehler(string? antwort) : IFilePicker
        {
            public string? AskForSaveTarget(string titel, string vorschlag, string filter) => null;

            public string? AskForExistingFile(string titel, string filter) => antwort;

            public string? AskForFolder(string titel, string start) => null;
        }
    }
}
