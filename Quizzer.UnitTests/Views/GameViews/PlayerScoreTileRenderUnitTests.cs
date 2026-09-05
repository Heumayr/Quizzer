using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Punkteleiste auf dem Beamer, wirklich ausgelegt und nachgemessen.
    /// <para>
    /// Der Grund fuer diese Probe: die hochformatige Spielerkarte wurde in der flachen Leiste auf
    /// ein Zwanzigstel gestaucht - die Punktzahl kam bei 1920x1080 mit rund 14 Bildpunkten an und
    /// war vom Sofa aus nicht zu lesen. Eine Zusicherung auf die Schriftgroesse im XAML wuerde das
    /// nicht bemerken, weil die Stauchung erst beim Auslegen entsteht.
    /// </para>
    /// </summary>
    [TestClass]
    public class PlayerScoreTileRenderUnitTests
    {
        /// <summary>Die Leiste ist rund ein Fuenftel eines 1080er Bildschirms.</summary>
        private const double LeisteBreite = 1900;
        private const double LeisteHoehe = 200;

        /// <summary>
        /// Fuehrt die Arbeit auf dem gemeinsamen Oberflaechen-Thread aus.
        /// <para>
        /// Frueher legte diese Klasse einen eigenen STA-Thread an und fuhr dessen Dispatcher
        /// am Ende herunter. <c>Application.Current</c> ist prozessweit - danach fand keine
        /// spaetere Klasse mehr einen lebenden Dispatcher. Einzelheiten in
        /// <see cref="UiTestHost"/>.
        /// </para>
        /// </summary>
        private static void OnUiThread(Action action) => UiTestHost.Run(action);

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

        /// <summary>
        /// Wie hoch der Text nach allen Stauchungen tatsaechlich auf dem Bildschirm ankommt.
        /// Die Schriftgroesse allein sagt das nicht - jede Viewbox darueber skaliert mit.
        /// </summary>
        private static double EffektiveHoehe(TextBlock text)
        {
            var hoehe = text.ActualHeight;
            DependencyObject? current = text;

            while (current != null)
            {
                if (current is Visual visual)
                {
                    var transform = VisualTreeHelper.GetTransform(visual);

                    if (transform is ScaleTransform skala)
                        hoehe *= skala.ScaleY;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return hoehe;
        }

        private static StatsContext BuildStats(int spielerzahl)
        {
            var game = new Game { Id = Guid.NewGuid(), Designation = "Testabend" };
            var context = new StatsContext();

            for (var i = 0; i < spielerzahl; i++)
            {
                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    Designation = $"Spieler{i + 1}",
                    DisplayName = i == 0 ? "The Pvement Punisher" : $"Spieler {i + 1}",
                };

                game.PlayerXGames.Add(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    PlayerId = player.Id,
                    Player = player,
                });
            }

            context.Game = game;

            context.PlayerStatsContextList = game.Players
                .Select(p => new PlayerStatsContext { Player = p, Game = game, StatsContext = context })
                .ToList();

            return context;
        }

        /// <summary>
        /// Fuenf Spieler auf einem 1080er Bildschirm: die Punktzahl muss aus mehreren Metern
        /// lesbar sein. Vorher waren es rund 14 Bildpunkte.
        /// </summary>
        [TestMethod]
        public void TheScoreIsBigEnoughToReadFromTheCouch()
        {
            OnUiThread(() =>
            {
                var view = new UcStatsView { DataContext = BuildStats(5) };

                view.Measure(new Size(LeisteBreite, LeisteHoehe));
                view.Arrange(new Rect(0, 0, LeisteBreite, LeisteHoehe));
                view.UpdateLayout();

                var hoehen = Descendants<TextBlock>(view)
                    .Where(t => t.ActualHeight > 0)
                    .Select(EffektiveHoehe)
                    .ToList();

                Assert.IsTrue(hoehen.Count > 0, "In der Punkteleiste ist gar kein Text angekommen.");

                var groesste = hoehen.Max();

                Assert.IsTrue(groesste >= 40,
                    $"Der groesste Text der Punkteleiste ist nur {groesste:0.#} Bildpunkte hoch - "
                    + "aus mehreren Metern nicht zu lesen.");
            });
        }

        /// <summary>
        /// Die Leiste wird auch ausgefuellt: die Kacheln nehmen die ganze Breite ein, statt in
        /// der Mitte zusammenzurutschen.
        /// </summary>
        [TestMethod]
        public void TheTilesFillTheWholeBar()
        {
            OnUiThread(() =>
            {
                var view = new UcStatsView { DataContext = BuildStats(5) };

                view.Measure(new Size(LeisteBreite, LeisteHoehe));
                view.Arrange(new Rect(0, 0, LeisteBreite, LeisteHoehe));
                view.UpdateLayout();

                var kacheln = Descendants<UcPlayerScoreTile>(view).ToList();

                Assert.AreEqual(5, kacheln.Count, "Es fehlt eine Kachel.");

                var belegt = kacheln.Sum(k => k.ActualWidth);

                Assert.IsTrue(belegt >= LeisteBreite * 0.9,
                    $"Die Kacheln belegen nur {belegt:0} von {LeisteBreite:0} Bildpunkten.");
            });
        }

        /// <summary>Auch mit acht Spielern bleibt die Punktzahl lesbar.</summary>
        [TestMethod]
        public void WithManyPlayersTheScoreStaysReadable()
        {
            OnUiThread(() =>
            {
                var view = new UcStatsView { DataContext = BuildStats(8) };

                view.Measure(new Size(LeisteBreite, LeisteHoehe));
                view.Arrange(new Rect(0, 0, LeisteBreite, LeisteHoehe));
                view.UpdateLayout();

                var groesste = Descendants<TextBlock>(view)
                    .Where(t => t.ActualHeight > 0)
                    .Select(EffektiveHoehe)
                    .DefaultIfEmpty(0)
                    .Max();

                Assert.IsTrue(groesste >= 28,
                    $"Bei acht Spielern ist der groesste Text nur {groesste:0.#} Bildpunkte hoch.");
            });
        }
    }
}
