using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Jedes Fenster der Anwendung laesst sich aufbauen und auslegen.
    /// <para>
    /// <b>Was dieser Test misst:</b> dass kein Fenster beim Erzeugen wirft und dass keine Bindung
    /// auf der obersten Ebene ins Leere greift. Die fuenfzehn Fenster bringen ihren DataContext
    /// im XAML mit, sind also ohne Datenbank aufbaubar.
    /// </para>
    /// <para>
    /// <b>Was er nicht misst:</b> Bindungen in Vorlagen, die erst mit Daten ausgewertet werden.
    /// Ein leeres <c>ItemsControl</c> erzeugt keine Zeile, und eine tote Bindung darin bleibt
    /// unsichtbar - genau so versteckte sich die auf <c>CellClickCommand</c> im Beamer-Fenster,
    /// bis <c>PlayerWindowRenderUnitTests</c> ein gefuelltes Raster hineingab. Wer ein Fenster
    /// wirklich absichern will, braucht dort einen eigenen Test mit Daten.
    /// </para>
    /// </summary>
    [TestClass]
    public class AllWindowsBuildUnitTests
    {
        private static readonly Type[] Fenster =
        [
            typeof(Quizzer.MainWindow),
            typeof(Quizzer.Views.CategoriesView),
            typeof(Quizzer.Views.EditGameView),
            typeof(Quizzer.Views.EditPlayerView),
            typeof(Quizzer.Views.GamesView),
            typeof(Quizzer.Views.GameViews.GameMasterView),
            typeof(Quizzer.Views.GameViews.GamePlayerView),
            typeof(Quizzer.Views.GameViews.PlayersResultView),
            typeof(Quizzer.Views.GameViews.QuestionMasterView),
            typeof(Quizzer.Views.HelperViewModels.QuestionSelectorView),
            typeof(Quizzer.Views.PlayersView),
            typeof(Quizzer.Views.QuestionsView),
            typeof(Quizzer.Views.QuestionTypes.EditQuestionsView),
            typeof(Quizzer.Views.QuestionTypes.EditStepView),
            typeof(Quizzer.Views.QuestionTypes.NewQuestionView),
        ];

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

        /// <summary>Baut alle Fenster auf einem STA-Thread und sammelt, was schiefging.</summary>
        private static List<string> BuildAll(out int gezeichnet)
        {
            var funde = new List<string>();
            var mitInhalt = 0;

            UiTestHost.Run(() =>
            {
                foreach (var typ in Fenster)
                {
                    try
                    {
                        using var wache = new BindingErrorWatch();

                        var fenster = (Window)Activator.CreateInstance(typ)!;
                        var inhalt = (FrameworkElement)fenster.Content;

                        inhalt.Measure(new Size(1200, 800));
                        inhalt.Arrange(new Rect(0, 0, 1200, 800));
                        inhalt.UpdateLayout();

                        if (Descendants<FrameworkElement>(inhalt).Any(e => e.ActualWidth > 0 && e.ActualHeight > 0))
                            mitInhalt++;

                        foreach (var fehler in wache.Errors)
                            funde.Add($"{typ.Name}: {fehler}");
                    }
                    catch (Exception ex)
                    {
                        funde.Add($"{typ.Name}: Aufbau gescheitert - {ex.GetType().Name}: {ex.Message}");
                    }
                }
            });

            gezeichnet = mitInhalt;

            return funde;
        }

        /// <summary>
        /// Kein Fenster wirft beim Aufbau, und keine Bindung greift ins Leere.
        /// </summary>
        [TestMethod]
        public void EveryWindowBuildsWithoutDeadBindings()
        {
            var funde = BuildAll(out var gezeichnet);

            // Die Gegenrichtung gleich mit: waeren die Fenster leer geblieben, koennte gar nichts
            // melden - dann sagt die Zusicherung oben nichts aus.
            Assert.AreEqual(Fenster.Length, gezeichnet,
                $"Nur {gezeichnet} von {Fenster.Length} Fenstern haben ueberhaupt etwas gezeichnet.");

            Assert.AreEqual(0, funde.Count,
                "Beim Aufbau der Fenster ist etwas schiefgegangen:" + Environment.NewLine
                + string.Join(Environment.NewLine, funde));
        }
    }
}
