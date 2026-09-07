using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Ein Medium, das sich nicht abspielen lässt.
    /// <para>
    /// <b>Gemessen 2026-09-07: es gab im ganzen Projekt keinen einzigen
    /// <c>MediaFailed</c>-Behandler.</b> <c>MediaElement</c> wirft dabei nicht — das Ereignis
    /// läuft die Baumhierarchie hoch, und wenn es niemand nimmt, geschieht <b>nichts</b>. Der
    /// Spielleiter drückt „Abspielen", es passiert nichts, und niemand erfährt warum. Genau so
    /// verhält sich ein <c>.webm</c> ohne die Windows-Erweiterung — der Fall, der beim
    /// Nachziehen der Medienendungen ausdrücklich gestoppt wurde.
    /// </para>
    /// </summary>
    [TestClass]
    public class UnspielbaresMediumUnitTests
    {
        private static string kaputt = string.Empty;

        [ClassInitialize]
        public static void SetUp(TestContext _)
        {
            kaputt = Path.Combine(Path.GetTempPath(), $"quizzer-kaputt-{Guid.NewGuid():N}.mp4");

            // Kein Video, sondern Text mit der Endung eines Videos. Genau das ist der Fall, den
            // MediaElement mit MediaFailed beantwortet.
            File.WriteAllText(kaputt, "Das ist kein Video.");
        }

        [ClassCleanup]
        public static void TearDown()
        {
            if (File.Exists(kaputt))
                File.Delete(kaputt);
        }

        /// <summary>
        /// Pumpt die Oberfläche, bis die Bedingung greift oder die Zeit um ist.
        /// <para>
        /// <c>MediaElement</c> lädt nebenher; ein blockierendes Warten auf dem Oberflächenfaden
        /// bekäme das Ereignis nie zu sehen.
        /// </para>
        /// </summary>
        private static void WarteBis(Func<bool> bedingung, TimeSpan grenze)
        {
            var rahmen = new DispatcherFrame();
            var bis = DateTime.UtcNow + grenze;

            var uhr = new DispatcherTimer(
                TimeSpan.FromMilliseconds(25),
                DispatcherPriority.Background,
                (_, _) =>
                {
                    if (bedingung() || DateTime.UtcNow > bis)
                        rahmen.Continue = false;
                },
                Dispatcher.CurrentDispatcher);

            uhr.Start();

            Dispatcher.PushFrame(rahmen);

            uhr.Stop();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }

        /// <summary>Der einzige sichtbare Text im Vorschaufenster, oder leer.</summary>
        private static string SichtbarerText(DependencyObject wurzel)
            => string.Join(" ", Descendants<TextBlock>(wurzel)
                .Where(t => t.Visibility == Visibility.Visible)
                .Select(t => t.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t)));

        /// <summary>
        /// <b>Das Vorschaufenster sagt es.</b> Ohne den Behandler bleibt es schwarz und stumm,
        /// und der Spielleiter steht vor einem Fenster, das nichts tut.
        /// </summary>
        [TestMethod]
        public void AnUnplayableVideoSaysSoInThePreviewWindow()
        {
            var gelesen = string.Empty;

            UiTestHost.Run(() =>
            {
                var fenster = new MediaPreviewWindow(
                    Guid.NewGuid(), kaputt, ResourceType.Video, isControlWindow: true);

                try
                {
                    fenster.Show();

                    var inhalt = (FrameworkElement)fenster.Content;

                    inhalt.Measure(new Size(800, 600));
                    inhalt.Arrange(new Rect(0, 0, 800, 600));
                    inhalt.UpdateLayout();

                    WarteBis(
                        () => SichtbarerText(inhalt).Contains("abspielen", StringComparison.Ordinal),
                        TimeSpan.FromSeconds(10));

                    gelesen = SichtbarerText(inhalt);
                }
                finally
                {
                    fenster.Close();
                }
            });

            StringAssert.Contains(gelesen, "lässt sich nicht abspielen",
                "Das Fenster bleibt schwarz und stumm - MediaElement wirft nicht, es meldet "
                + "ueber MediaFailed, und ohne Behandler geschieht gar nichts. Gelesen wurde: ["
                + gelesen + "]");

            StringAssert.Contains(gelesen, Path.GetFileName(kaputt),
                "Die Meldung nennt die Datei nicht: [" + gelesen + "]");
        }
        /// <summary>
        /// <b>Und dieselbe Stelle im Spiel selbst.</b> Der Schritt-Betrachter ist der wichtigere
        /// der beiden: er steht während des Abends auf dem Spielleiter-Bildschirm <i>und</i> am
        /// Beamer. Das Vorschaufenster geht nur im Aufbau auf.
        /// </summary>
        [TestMethod]
        public void AnUnplayableVideoSaysSoInTheStepView()
        {
            var gelesen = string.Empty;

            UiTestHost.Run(() =>
            {
                var betrachter = new ResourceViewerControl
                {
                    RootFolder = Path.GetDirectoryName(kaputt),
                    IsMasterView = true,
                };

                var fenster = new Window { Content = betrachter, Width = 800, Height = 600 };

                try
                {
                    fenster.Show();

                    betrachter.DataContext = new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        ResourceTyp = ResourceType.Video,
                        ResourceFileName = Path.GetFileName(kaputt),
                    };

                    betrachter.RefreshView();

                    betrachter.Measure(new Size(800, 600));
                    betrachter.Arrange(new Rect(0, 0, 800, 600));
                    betrachter.UpdateLayout();

                    WarteBis(
                        () => SichtbarerText(betrachter).Contains("abspielen", StringComparison.Ordinal),
                        TimeSpan.FromSeconds(10));

                    gelesen = SichtbarerText(betrachter);
                }
                finally
                {
                    fenster.Close();
                }
            });

            StringAssert.Contains(gelesen, "lässt sich nicht abspielen",
                "Im Spiel bleibt die Flaeche schwarz und stumm. Gelesen wurde: [" + gelesen + "]");

            StringAssert.Contains(gelesen, Path.GetFileName(kaputt),
                "Die Meldung nennt die Datei nicht: [" + gelesen + "]");
        }

    }
}
