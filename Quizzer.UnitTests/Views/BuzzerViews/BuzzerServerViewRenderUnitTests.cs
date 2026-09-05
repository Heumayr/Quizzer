using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.BuzzerViews.Firewall;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.BuzzerViews
{
    /// <summary>
    /// Baut das Buzzer-Server-Fenster wirklich auf und sieht nach, was der Spielleiter liest.
    /// Eine Zusicherung auf die Properties allein wuerde eine tote Bindung nicht bemerken.
    /// </summary>
    [TestClass]
    public class BuzzerServerViewRenderUnitTests
    {
        /// <summary>Eine Firewall-Lage nach Vorgabe, ohne echte Firewall.</summary>
        private sealed class FakeFirewallReader : IFirewallReader
        {
            public required int ActiveProfiles { get; init; }

            public required IReadOnlyList<FirewallRule> Rules { get; init; }

            public bool IsAvailable => true;

            public IReadOnlyList<FirewallRule> ReadRules() => Rules;
        }

        private static void OnUiThread(Action action)
        {
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        var app = new Quizzer.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        app.InitializeComponent();
                    }

                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(60)), "Der Aufbau blieb haengen.");

            if (failure != null)
                throw new AssertFailedException(
                    $"Die Ansicht liess sich nicht aufbauen: {failure.Message}", failure);
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

        /// <summary>Baut das Fenster mit dem gegebenen ViewModel und legt seinen Inhalt aus.</summary>
        private static FrameworkElement Build(BuzzerServerViewModel vm)
        {
            var window = new BuzzerServerView { DataContext = vm };

            var content = (FrameworkElement)window.Content;

            content.Measure(new Size(900, 600));
            content.Arrange(new Rect(0, 0, 900, 600));
            content.UpdateLayout();

            return content;
        }

        private static BuzzerServerViewModel BuildViewModel()
        {
            var anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            var bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = string.Empty };

            var game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };

            foreach (var player in new[] { anna, bert })
            {
                game.PlayerXGames.Add(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    PlayerId = player.Id,
                    Player = player,
                });
            }

            return new BuzzerServerViewModel { Game = game };
        }

        /// <summary>
        /// Sichtbar heisst: das Element und jeder Vorfahre stehen auf
        /// <see cref="Visibility.Visible"/> und haben eine Flaeche bekommen.
        /// <para>
        /// Ein ausgeblendeter Knopf steht weiterhin im visuellen Baum. Ohne diese Pruefung waere
        /// die Gegenrichtung ("keine Warnung, wenn die Freigabe passt") nie rot geworden -
        /// gemessen beim ersten Lauf.
        /// </para>
        /// </summary>
        private static bool IsLaidOutAndVisible(FrameworkElement element)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return false;

            DependencyObject? current = element;

            while (current != null)
            {
                if (current is UIElement ui && ui.Visibility != Visibility.Visible)
                    return false;

                current = VisualTreeHelper.GetParent(current);
            }

            return true;
        }

        /// <summary>Alles, was der Spielleiter tatsaechlich zu sehen bekommt.</summary>
        private static IEnumerable<string> Texts(FrameworkElement root) =>
            Descendants<TextBlock>(root).Where(IsLaidOutAndVisible).Select(t => t.Text)
                .Concat(Descendants<Button>(root).Where(IsLaidOutAndVisible)
                    .Select(b => b.Content?.ToString() ?? string.Empty));

        [TestMethod]
        public void TheWindowIsNamedAndSpeaksGerman()
        {
            OnUiThread(() =>
            {
                var vm = BuildViewModel();
                var window = new BuzzerServerView { DataContext = vm };

                Assert.AreEqual("Buzzer-Server", window.Title,
                    "Das Fenster hiess in der Taskleiste zuletzt \"Window1\".");

                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(900, 600));
                content.Arrange(new Rect(0, 0, 900, 600));
                content.UpdateLayout();

                var texte = Texts(content).ToList();

                Assert.IsTrue(texte.Count > 0, "Es ist gar nichts sichtbar angekommen.");

                CollectionAssert.Contains(texte, "Server starten");
                CollectionAssert.Contains(texte, "Server beenden");

                Assert.IsFalse(texte.Any(t => t.Contains("Start Server") || t.Contains("Stop Server")),
                    "Es stehen noch englische Beschriftungen im Fenster.");
            });
        }

        /// <summary>
        /// Ohne laufenden Server muss ein Satz dastehen, kein Enum-Name. "None" war bisher das,
        /// was der Spielleiter vor dem Start las.
        /// </summary>
        [TestMethod]
        public void WithoutAServerTheStateIsASentence()
        {
            OnUiThread(() =>
            {
                var content = Build(BuildViewModel());

                var texte = Texts(content).ToList();

                CollectionAssert.Contains(texte, "Server nicht gestartet");

                Assert.IsFalse(texte.Contains("None"), "Der rohe Enum-Name steht noch im Fenster.");
            });
        }

        /// <summary>
        /// Die Spielertabelle: nur die drei gemeinten Spalten, und der Verbindungszustand als
        /// Wort. Automatisch erzeugt waren es elf, darunter Kennung und Bilddaten.
        /// </summary>
        [TestMethod]
        public void ThePlayerTableShowsThreeMeaningfulColumns()
        {
            OnUiThread(() =>
            {
                var content = Build(BuildViewModel());

                var grid = Descendants<DataGrid>(content).SingleOrDefault();

                Assert.IsNotNull(grid, "Die Spielertabelle fehlt.");

                Assert.IsFalse(grid!.AutoGenerateColumns,
                    "Automatisch erzeugte Spalten zeigen Kennung, Bilddaten und Ergebnisliste.");

                var kopfzeilen = grid.Columns.Select(c => c.Header?.ToString()).ToList();

                CollectionAssert.AreEqual(
                    new[] { "Spieler", "Verbindung", "QR-Code" }, kopfzeilen);
            });
        }

        /// <summary>
        /// Der Kern der gemeldeten Stoerung: gilt die Firewall-Regel nicht fuer das aktive Netz,
        /// muss das im Fenster stehen - samt Knopf, der es beheben kann.
        /// </summary>
        [TestMethod]
        public void ABlockingFirewallIsNamedInTheWindow()
        {
            OnUiThread(() =>
            {
                var vm = BuildViewModel();

                // Die an diesem Rechner gemessene Lage: Regel nur fuer Oeffentlich (4),
                // aktives Netz Privat (2).
                vm.FirewallReader = new FakeFirewallReader
                {
                    ActiveProfiles = 2,
                    Rules = new[]
                    {
                        new FirewallRule("Quizzer", Environment.ProcessPath, true, true, true, 4, 6, "*"),
                    },
                };

                vm.ForceFirewallCheckForTest();

                var content = Build(vm);

                var texte = Texts(content).ToList();

                Assert.IsTrue(texte.Any(t => t.Contains("Privat")),
                    "Der Spielleiter erfaehrt nicht, dass die Freigabe fuer sein Netz fehlt. "
                    + "Sichtbar ist: " + string.Join(" | ", texte));

                CollectionAssert.Contains(texte, "Freigeben");
            });
        }

        /// <summary>
        /// Die Gegenrichtung, und sie ist die wichtigere Haelfte: passt die Freigabe, darf keine
        /// Warnung dastehen. Sonst gewoehnt man sich an einen Hinweis, der immer da ist.
        /// </summary>
        [TestMethod]
        public void WithAMatchingRuleNoWarningIsShown()
        {
            OnUiThread(() =>
            {
                var vm = BuildViewModel();

                vm.FirewallReader = new FakeFirewallReader
                {
                    ActiveProfiles = 2,
                    Rules = new[]
                    {
                        new FirewallRule("Quizzer", Environment.ProcessPath, true, true, true, 6, 6, "*"),
                    },
                };

                vm.ForceFirewallCheckForTest();

                var content = Build(vm);

                Assert.IsFalse(Texts(content).Any(t => t.Contains("Freigeben")),
                    "Es steht eine Firewall-Warnung da, obwohl die Freigabe passt.");
            });
        }
    }
}
