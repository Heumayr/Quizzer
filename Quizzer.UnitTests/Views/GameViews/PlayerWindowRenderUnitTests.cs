using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using Quizzer.ViewModels;
using Quizzer.Views.HelperViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static Quizzer.Views.HelperViewModels.GridBuilder;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Das Fenster, das auf dem Beamer steht - das einzige, das die Mitspieler sehen.
    /// <para>
    /// Es war bis 2026-09-06 das einzige Spielfenster ohne jede Zusicherung auf seinen Aufbau.
    /// Das ist die schlechteste Stelle dafuer: eine tote Bindung wirft nicht, sie bleibt still
    /// auf dem Standardwert stehen. Auf dem Spielleiter-Bildschirm faellt so etwas auf, weil
    /// dort jemand hinsieht - auf dem Beamer erst, wenn zwoelf Gaeste vor einer leeren Wand
    /// sitzen.
    /// </para>
    /// </summary>
    [TestClass]
    public class PlayerWindowRenderUnitTests
    {
        private const double Breite = 1600;
        private const double Hoehe = 900;

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

        private static FrameworkElement Layout(Window window)
        {
            var content = (FrameworkElement)window.Content;

            content.Measure(new Size(Breite, Hoehe));
            content.Arrange(new Rect(0, 0, Breite, Hoehe));
            content.UpdateLayout();

            return content;
        }

        /// <summary>
        /// Baut ein Spiel mit einem 2x2-Raster, ohne Datenbank. Der GridBuilder schreibt, dieser
        /// Test soll nur zeichnen.
        /// </summary>
        private static GamePlayerViewModel BuildViewModel()
        {
            var game = new Game
            {
                Id = Guid.NewGuid(),
                Designation = "Testabend",
                Phase = 1,
                SuggestedPhases = 2,
                Height = 2,
                Width = 2,
                CellHeight = 90,
                CellWidth = 160,
            };

            var gitter = new GameGridVMs();

            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 2; x++)
                {
                    var frage = new DefaultQuestion
                    {
                        Id = Guid.NewGuid(),
                        Designation = $"Frage {y}/{x}",
                        DesignationShort = $"F{y}{x}",
                        Points = 100,
                        MinusPoints = 50,
                        Difficulty = Difficulty.Level1,
                    };

                    var zelle = new GameGridCoordinate
                    {
                        Id = Guid.NewGuid(),
                        GameId = game.Id,
                        Game = game,
                        X = x,
                        Y = y,
                        Phase = 1,
                        QuestionBaseId = frage.Id,
                        QuestionBase = frage,
                    };

                    zelle.CalculateAndSetCurrentPoints();
                    game.GameGridCoordinates.Add(zelle);

                    gitter.CellVMs.Add(new GameGridCoordinateViewModel(zelle)
                    {
                        CellView = CellView.PlayField,
                    });
                }
            }

            return new GamePlayerViewModel
            {
                Game = game,
                GameGridVMs = gitter,
            };
        }

        /// <summary>
        /// Keine Bindung im Beamer-Fenster darf ins Leere greifen.
        /// </summary>
        [TestMethod]
        public void TheBeamerWindowHasNoDeadBindings()
        {
            OnUiThread(() =>
            {
                using var wache = new BindingErrorWatch();

                var fenster = new GamePlayerView { DataContext = BuildViewModel() };
                Layout(fenster);

                Assert.AreEqual(0, wache.Errors.Count,
                    "Im Beamer-Fenster greifen Bindungen ins Leere: "
                    + string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>
        /// Und es kommt wirklich etwas an. Ohne diese Zusicherung waere die vorige auch dann
        /// gruen, wenn das Fenster leer bliebe - wo nichts gebunden wird, meldet auch nichts.
        /// </summary>
        [TestMethod]
        public void TheBeamerWindowActuallyDrawsTheGrid()
        {
            OnUiThread(() =>
            {
                var vm = BuildViewModel();

                // Nicht 100: die Zelle zeigt den gerechneten Wert, nicht die Rohpunkte der
                // Frage. Erst gemessen, dann zugesichert - die erste Fassung dieses Tests
                // erwartete 100 und war deshalb rot, ohne dass etwas kaputt war.
                var erwartet = vm.Game!.GameGridCoordinates[0].CurrentPoints.ToString();

                var fenster = new GamePlayerView { DataContext = vm };
                var inhalt = Layout(fenster);

                var texte = Descendants<TextBlock>(inhalt)
                    .Where(t => t.ActualWidth > 0 && t.ActualHeight > 0)
                    .Select(t => t.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                Assert.IsTrue(texte.Count > 0,
                    "Auf dem Beamer steht gar nichts - das Raster ist nicht angekommen.");

                Assert.AreEqual(4, vm.GameGridVMs.CellVMs.Count,
                    "Das Testraster hat nicht vier Zellen - dann misst der Rest nichts.");

                Assert.IsTrue(texte.Count(t => t.Contains(erwartet)) >= 4,
                    $"Die Punktezahl {erwartet} steht nicht auf allen vier Zellen. Sichtbar ist: "
                    + string.Join(" | ", texte.Take(20)));
            });
        }
    }
}
