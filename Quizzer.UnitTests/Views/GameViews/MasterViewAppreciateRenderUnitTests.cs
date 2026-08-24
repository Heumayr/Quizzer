using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews;
using Quizzer.Views.StaticRessources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Baut die Spielleiteransicht mit einer Schaetzfrage wirklich auf und sieht nach, was dort
    /// sichtbar ankommt. Eine Zusicherung auf die Property allein wuerde das nicht zeigen:
    /// abgeschnitten oder auf Groesse null gelegt ist der Text vorhanden und trotzdem nicht da.
    /// </summary>
    [TestClass]
    public class MasterViewAppreciateRenderUnitTests
    {
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

        /// <summary>
        /// Sichtbar heisst hier: das Element und jeder Vorfahre stehen auf
        /// <see cref="Visibility.Visible"/> und haben eine Flaeche bekommen.
        /// <para>
        /// <c>IsVisible</c> taugt dafuer nicht - es ist bei einem Fenster, das nie
        /// <c>Show()</c> gesehen hat, immer <c>false</c>, und der Test waere gruen wie rot
        /// aus dem falschen Grund.
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

        /// <summary>Eine Schaetzfrage samt Zelle, wie sie das Spiel oeffnet.</summary>
        private static CurrentQuestionViewModel BuildViewModel()
        {
            var question = new AppreciateQestion
            {
                Id = Guid.NewGuid(),
                Designation = "Grossglockner",
                QuestionText = "Wie hoch ist der Grossglockner?",
                ValueKind = AppreciateValueKind.Length,
                Unit = AppreciateUnit.Meter,
                ExpectedValue = 3798,
            };

            question.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = question.Id,
                StepText = "Der hoechste Berg Oesterreichs.",
                SequenceNumber = 10,
            });

            question.CalculateOrderdSteps();

            return new CurrentQuestionViewModel
            {
                Coordinate = new GameGridCoordinate
                {
                    Id = Guid.NewGuid(),
                    QuestionBaseId = question.Id,
                    QuestionBase = question,
                },
            };
        }

        [TestMethod]
        public void TheMasterViewShowsTheExpectedValue()
        {
            OnUiThread(() =>
            {
                var vm = BuildViewModel();
                var window = new QuestionMasterView { DataContext = vm };

                // Ein Fenster ohne Show() legt nicht aus; sein Inhalt schon.
                var root = (FrameworkElement)window.Content;

                root.Measure(new Size(1600, 900));
                root.Arrange(new Rect(0, 0, 1600, 900));
                root.UpdateLayout();

                var texts = Descendants<TextBlock>(root)
                    .Where(IsLaidOutAndVisible)
                    .Select(t => t.Text)
                    .ToArray();

                Assert.IsTrue(texts.Any(t => t.Contains("3798")),
                    "Der Sollwert steht dem Spielleiter nirgends sichtbar vor Augen. "
                    + $"Sichtbar sind: {string.Join(" | ", texts)}");

                Assert.IsTrue(texts.Any(t => t.Contains("m")),
                    "Die Einheit fehlt - eine blanke Zahl sagt nicht, ob Meter oder Kilometer.");
            });
        }

        /// <summary>
        /// Die Steuerung des Spielleiters baut auf und zeigt, worauf er wartet. Ein gruener
        /// Build merkt von einer Bindung ins Leere nichts - die alte Fassung band auf ein
        /// <c>BuzzerState</c>, das es im ViewModel gar nicht gibt.
        /// </summary>
        [TestMethod]
        public void TheMastersBuzzerControlsShowTheRound()
        {
            OnUiThread(() =>
            {
                try
                {
                    var anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
                    var game = new Game { Id = Guid.NewGuid(), Designation = "Spiel", Phase = 1 };

                    game.PlayerXGames.Add(new PlayerXGame
                    {
                        Id = Guid.NewGuid(),
                        GameId = game.Id,
                        PlayerId = anna.Id,
                        Player = anna,
                    });

                    var serverVm = StaticManager.BuzzerServerViewModel;
                    serverVm.Game = game;

                    var vm = new BuzzerControlsViewModel();
                    vm.SetBuzzerVerverViewModel(serverVm);
                    vm.SetRoundInfo(new AppreciateQestion
                    {
                        Id = Guid.NewGuid(),
                        Designation = "Grossglockner",
                        ValueKind = AppreciateValueKind.Length,
                        Unit = AppreciateUnit.Meter,
                        ExpectedValue = 3798,
                    });

                    var view = new BuzzerControlsView { DataContext = vm };

                    view.Measure(new Size(600, 400));
                    view.Arrange(new Rect(0, 0, 600, 400));
                    view.UpdateLayout();

                    var texts = Descendants<TextBlock>(view)
                        .Where(IsLaidOutAndVisible)
                        .Select(t => t.Text)
                        .ToArray();

                    Assert.IsTrue(texts.Any(t => t.Contains("Anna")),
                        $"Der Mitspieler fehlt in der Uebersicht. Sichtbar: {string.Join(" | ", texts)}");
                    Assert.IsTrue(texts.Any(t => t.Contains("3798")),
                        $"Der Sollwert fehlt in der Steuerung. Sichtbar: {string.Join(" | ", texts)}");
                    Assert.IsTrue(texts.Any(t => t.Contains("Schaetzfrage")),
                        $"Die Kopfzeile nennt die Frage nicht. Sichtbar: {string.Join(" | ", texts)}");

                    // Sichtbarer Text traegt echte Umlaute (standards-allgemein.md, Abschnitt 1).
                    // Nachgemessen: das BOM der XAML ist dafuer nicht noetig, MSBuild liest sie
                    // auch ohne als UTF-8 - diese Zusicherung haelt die Regel fest, nicht die
                    // Kodierung.
                    Assert.IsTrue(texts.Any(t => t.Contains("Lösung")),
                        $"Der Beschriftung fehlt der echte Umlaut. Sichtbar: {string.Join(" | ", texts)}");
                }
                finally
                {
                    StaticManager.Reset();
                }
            });
        }
    }
}
