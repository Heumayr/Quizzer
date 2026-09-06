using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Das Medien-Vorschaufenster - das einzige Fenster ohne parameterlosen Konstruktor und
    /// deshalb nicht in <see cref="AllWindowsBuildUnitTests"/>.
    /// <para>
    /// <b>Gemessen am 2026-09-06 nachts:</b> es fehlte in <c>AllWindowsBuildUnitTests</c>
    /// <b>und</b> in <c>LesbarkeitUnitTests</c> - ein Fenster, das während des Abends aufgeht,
    /// war in keiner der beiden Prüfungen. Aufgefallen ist es nur durch eine Nachzählung.
    /// </para>
    /// </summary>
    [TestClass]
    public class MediaPreviewWindowUnitTests
    {
        private static string bildDatei = string.Empty;

        [ClassInitialize]
        public static void SetUp(TestContext _)
        {
            bildDatei = Path.Combine(Path.GetTempPath(), $"quizzer-probe-{Guid.NewGuid():N}.png");

            // Ein winziges echtes PNG - BitmapImage.EndInit wirft bei allem anderen.
            var bild = BitmapSource.Create(
                2, 2, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
                new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 }, 8);

            var kodierer = new PngBitmapEncoder();

            kodierer.Frames.Add(BitmapFrame.Create(bild));

            using var strom = File.Create(bildDatei);

            kodierer.Save(strom);
        }

        [ClassCleanup]
        public static void TearDown()
        {
            if (File.Exists(bildDatei))
                File.Delete(bildDatei);
        }

        private static MediaPreviewWindow Baue(bool mitLeiste)
            => new(Guid.NewGuid(), bildDatei, ResourceType.Image, mitLeiste);

        /// <summary>
        /// Es baut sich auf, legt sich aus und zeigt das Bild - ohne zu werfen.
        /// </summary>
        [TestMethod]
        public void ItBuildsAndShowsTheImage()
        {
            UiTestHost.Run(() =>
            {
                var fenster = Baue(mitLeiste: true);

                try
                {
                    // Das Bild wird erst im Loaded-Ereignis geladen; ohne Zeigen laeuft es nie.
                    fenster.Show();

                    var inhalt = (FrameworkElement)fenster.Content;

                    inhalt.Measure(new Size(800, 600));
                    inhalt.Arrange(new Rect(0, 0, 800, 600));
                    inhalt.UpdateLayout();

                    Assert.IsTrue(fenster.IsLoaded, "Das Fenster wurde nicht geladen.");
                }
                finally
                {
                    fenster.Close();
                }
            });
        }

        /// <summary>
        /// <b>Die Knopfleiste erscheint nur im Steuerfenster.</b> Bei mehreren Bildschirmen ist
        /// genau eines das Steuerfenster; auf dem Beamer hätten „Alle starten" und
        /// „Alle schließen" vor den Gästen nichts zu suchen.
        /// </summary>
        [TestMethod]
        public void TheControlBarAppearsOnlyOnTheControlWindow()
        {
            var sichtbarkeiten = new List<Visibility>();

            UiTestHost.Run(() =>
            {
                foreach (var mitLeiste in new[] { true, false })
                {
                    var fenster = Baue(mitLeiste);

                    try
                    {
                        fenster.Show();

                        var leiste = (FrameworkElement)fenster.FindName("ControlBar");

                        Assert.IsNotNull(leiste,
                            "ControlBar gibt es nicht mehr - dann misst diese Probe nichts.");

                        sichtbarkeiten.Add(leiste.Visibility);
                    }
                    finally
                    {
                        fenster.Close();
                    }
                }
            });

            Assert.AreEqual(Visibility.Visible, sichtbarkeiten[0],
                "Das Steuerfenster zeigt seine Knopfleiste nicht.");

            Assert.AreEqual(Visibility.Collapsed, sichtbarkeiten[1],
                "Die Knopfleiste steht auch auf dem Beamer - dort gehoert sie nicht hin.");
        }
    }
}
