using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using System.IO;
using System.Windows;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Eine Bilddatei, die da ist und sich trotzdem nicht lesen lässt.
    /// <para>
    /// <b>Gemessen am 2026-09-06 nachts mit einer Sonde, nicht behauptet:</b>
    /// <c>BitmapImage.EndInit()</c> wirft dann
    /// <c>NotSupportedException: Es wurde keine passende Imagingkomponente zum Abschließen
    /// dieses Vorgangs gefunden</c> - mitten im Aufbau des Schrittes. Fünf Stellen taten das
    /// ungeschützt, drei davon auf dem Weg zum Beamer.
    /// </para>
    /// <para>
    /// <b><c>File.Exists</c> genügt nicht</b>, und es stand an drei dieser Stellen bereits davor.
    /// Auslöser im Betrieb: eine <c>.webp</c>-Datei auf einem Rechner ohne deren Codec - die
    /// Dateiauswahl des Frageneditors bietet <c>.webp</c> ausdrücklich an -, eine halb kopierte
    /// Datei, eine umbenannte.
    /// </para>
    /// </summary>
    [TestClass]
    public class UnlesbaresBildUnitTests
    {
        private static string ordner = string.Empty;
        private const string Kaputt = "kaputt.png";

        [ClassInitialize]
        public static void SetUp(TestContext _)
        {
            ordner = Path.Combine(Path.GetTempPath(), "quizzer-unlesbar-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(ordner);

            // Endung stimmt, Inhalt nicht - genau der Fall, den File.Exists durchlaesst.
            File.WriteAllText(Path.Combine(ordner, Kaputt), "das ist kein PNG");
        }

        [ClassCleanup]
        public static void TearDown()
        {
            if (Directory.Exists(ordner))
                Directory.Delete(ordner, true);
        }

        /// <summary>Der Lader liefert nichts zurück, statt zu werfen.</summary>
        [TestMethod]
        public void TheLoaderReturnsNothingInsteadOfThrowing()
        {
            Assert.IsNull(Bildlader.Lade(Path.Combine(ordner, Kaputt)),
                "Eine unlesbare Datei liefert ein Bild - dann misst diese Probe nichts.");

            Assert.IsNull(Bildlader.Lade(Path.Combine(ordner, "gibtesgarnicht.png")),
                "Eine fehlende Datei muss ebenso nichts liefern.");

            Assert.IsNull(Bildlader.Lade(null), "Ein leerer Pfad muss nichts liefern.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn der Lader
        /// <i>immer</i> nichts lieferte - dann sähe man am Beamer nie ein Bild.
        /// </summary>
        [TestMethod]
        public void AReadableImageStillComesThrough()
        {
            var gut = Path.Combine(ordner, "gut.png");

            using (var strom = File.Create(gut))
            {
                var bild = System.Windows.Media.Imaging.BitmapSource.Create(
                    1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
                    new byte[] { 0, 0, 255, 255 }, 4);

                var kodierer = new System.Windows.Media.Imaging.PngBitmapEncoder();

                kodierer.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bild));
                kodierer.Save(strom);
            }

            Assert.IsNotNull(Bildlader.Lade(gut),
                "Auch ein lesbares Bild kommt nicht durch - dann zeigt der Beamer nie eines.");
        }

        /// <summary>
        /// Das Medien-Steuerelement des Fragefensters wirft nicht mehr - es sagt, was los ist.
        /// </summary>
        [TestMethod]
        public void TheStepViewShowsAMessageInsteadOfThrowing()
        {
            string? hinweis = null;
            Exception? geworfen = null;

            UiTestHost.Run(() =>
            {
                try
                {
                    var steuer = new ResourceViewerControl { RootFolder = ordner };

                    steuer.DataContext = new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        ResourceFileName = Kaputt,
                        ResourceTyp = ResourceType.Image,
                        StepText = "Probe",
                    };

                    steuer.RefreshView();

                    var feld = (System.Windows.Controls.TextBlock)steuer.FindName("MediaHinweis");

                    if (feld.Visibility == Visibility.Visible)
                        hinweis = feld.Text;
                }
                catch (Exception ex)
                {
                    geworfen = ex;
                }
            });

            Assert.IsNull(geworfen,
                "Der Schrittaufbau wirft weiterhin - am Abend heisst das ein Fehlerfenster vor "
                + "den Gaesten statt des Schrittes. Ausnahme: " + geworfen);

            Assert.IsNotNull(hinweis,
                "Es wird nichts angezeigt. Der Spielleiter sieht eine leere Flaeche und weiss "
                + "nicht, ob das Absicht ist.");

            StringAssert.Contains(hinweis!, Kaputt,
                "Der Hinweis nennt die Datei nicht - dann weiss niemand, welche zu ersetzen ist: "
                + hinweis);
        }

        /// <summary>
        /// Und das Vorschaufenster ebenso - es geht während des Abends auf.
        /// </summary>
        [TestMethod]
        public void ThePreviewWindowShowsAMessageInsteadOfThrowing()
        {
            string? hinweis = null;
            Exception? geworfen = null;

            UiTestHost.Run(() =>
            {
                var fenster = new MediaPreviewWindow(
                    Guid.NewGuid(), Path.Combine(ordner, Kaputt), ResourceType.Image, true);

                try
                {
                    fenster.Show();

                    var feld = (System.Windows.Controls.TextBlock)fenster.FindName("Hinweis");

                    if (feld.Visibility == Visibility.Visible)
                        hinweis = feld.Text;
                }
                catch (Exception ex)
                {
                    geworfen = ex;
                }
                finally
                {
                    fenster.Close();
                }
            });

            Assert.IsNull(geworfen, "Das Vorschaufenster wirft weiterhin. Ausnahme: " + geworfen);

            Assert.IsNotNull(hinweis, "Das Vorschaufenster bleibt schwarz und sagt nichts.");
        }
    }
}
