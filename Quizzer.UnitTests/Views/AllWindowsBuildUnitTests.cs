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
            typeof(Quizzer.Views.QuestionTypes.NewQuestionView),
            typeof(Quizzer.Views.BuzzerViews.BuzzerServerView),
            typeof(Quizzer.Views.GameThemesView),
            typeof(Quizzer.Views.LoginView),
            typeof(Quizzer.Views.SettingsView),
            typeof(Quizzer.Views.ImportPlayersView),
            typeof(Quizzer.Views.QuestionTypes.QuestionPreviewView),
            typeof(Quizzer.Views.QuestionTypes.RevealEditorView),
        ];

        /// <summary>
        /// Fenster, die <b>nicht</b> in der Liste stehen duerfen - jedes mit seinem Grund im
        /// Klartext.
        /// </summary>
        private static readonly Dictionary<string, string> Ausnahmen = new()
        {
            [nameof(Quizzer.Views.GameViews.QuestionViews.Typed.Media.MediaPreviewWindow)] =
                "hat keinen parameterlosen Konstruktor - es braucht Datei, Typ und Gruppe. "
                + "Eigener Test: MediaPreviewWindowUnitTests",
        };

        /// <summary>
        /// <b>Die Liste oben ist vollstaendig.</b>
        /// <para>
        /// Sie wird von Hand gepflegt, und genau so entsteht eine Luecke: gemessen am
        /// 2026-09-06 nachts fehlte <c>MediaPreviewWindow</c> in dieser Liste <b>und</b> in
        /// <c>LesbarkeitUnitTests</c> - ein Fenster, das waehrend des Abends aufgeht, war in
        /// keiner der beiden Pruefungen. Aufgefallen ist es nur, weil jemand nachgezaehlt hat.
        /// </para>
        /// <para>
        /// Diese Zusicherung zaehlt jetzt fuer alle nach: ein neues Fenster ist rot, bis es in
        /// der Liste steht oder mit Grund in den Ausnahmen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheListCoversEveryWindowOfTheApplication()
        {
            var alle = typeof(Quizzer.MainWindow).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(Window).IsAssignableFrom(t))
                .Where(t => t != typeof(Quizzer.Base.WindowBase))
                .ToList();

            Assert.IsTrue(alle.Count >= 20,
                $"Es wurden nur {alle.Count} Fenster gefunden - die Suche greift nicht mehr, "
                + "und die Zusicherung waere gruen, ohne etwas zu messen.");

            var fehlend = alle
                .Where(t => !Fenster.Contains(t) && !Ausnahmen.ContainsKey(t.Name))
                .Select(t => t.FullName ?? t.Name)
                .ToList();

            Assert.AreEqual(0, fehlend.Count,
                "Diese Fenster stehen in keiner Liste - sie werden also nie aufgebaut und nie "
                + "auf Lesbarkeit geprueft. Entweder gehoeren sie in Fenster[], oder der Grund "
                + "dagegen gehoert in Ausnahmen: " + string.Join(", ", fehlend));
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

                        // Schliessen nicht vergessen: WPF haelt jedes erzeugte Fenster in
                        // Application.Windows fest, und der Oberflaechen-Thread lebt bis zum
                        // Prozessende. Ohne das sammeln sich Fenster ueber den ganzen Testlauf.
                        using var wegraeumen = new FensterAufraeumer(typ);

                        var fenster = wegraeumen.Fenster;
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

        /// <summary>
        /// Kein Fenster ragt in seiner <b>eigenen</b> Groesse waagrecht heraus.
        /// <para>
        /// Deutsche Beschriftungen sind laenger als englische. Beim Uebersetzen der
        /// Verwaltungsfenster am 2026-09-06 ragte der Spieleditor sofort heraus: zwei
        /// ausgeschriebene Faktoren-Beschriftungen nebeneinander brauchten rund 730 Bildpunkte
        /// bei 800 Fensterbreite. Zwei weitere Fenster lagen schon vorher 35 Bildpunkte
        /// darueber. Auf dem Bildschirm sieht man das nicht - man zieht das Fenster groesser,
        /// ohne es zu merken.
        /// </para>
        /// </summary>
        [TestMethod]
        public void NoWindowOverflowsItsOwnWidth()
        {
            var funde = new List<string>();
            var gemessen = 0;

            UiTestHost.Run(() =>
            {
                foreach (var typ in Fenster)
                {
                    var fenster = (Window)Activator.CreateInstance(typ)!;

                    // Ohne eigene Angabe im XAML die WPF-Standardgroesse.
                    var breite = double.IsNaN(fenster.Width) ? 800 : fenster.Width;
                    var hoehe = double.IsNaN(fenster.Height) ? 450 : fenster.Height;

                    var inhalt = (FrameworkElement)fenster.Content;

                    inhalt.Measure(new Size(breite, hoehe));
                    inhalt.Arrange(new Rect(0, 0, breite, hoehe));
                    inhalt.UpdateLayout();

                    gemessen++;

                    var ueber = Descendants<FrameworkElement>(inhalt)
                        .Where(e => e.ActualWidth > 0 && e.ActualHeight > 0)
                        .Select(e => new { e, x = e.TranslatePoint(new Point(0, 0), inhalt).X })
                        .Where(a => a.x + a.e.ActualWidth > breite + 1)
                        .Select(a => $"{a.e.GetType().Name} bis {a.x + a.e.ActualWidth:0}")
                        .Distinct()
                        .Take(3)
                        .ToList();

                    if (ueber.Count > 0)
                        funde.Add($"{typ.Name} ({breite:0} breit): " + string.Join(" ; ", ueber));
                }
            });

            Assert.AreEqual(Fenster.Length, gemessen,
                $"Nur {gemessen} von {Fenster.Length} Fenstern gemessen.");

            Assert.AreEqual(0, funde.Count,
                "Diese Fenster ragen in ihrer eigenen Groesse heraus:" + Environment.NewLine
                + string.Join(Environment.NewLine, funde));
        }
    }
}
