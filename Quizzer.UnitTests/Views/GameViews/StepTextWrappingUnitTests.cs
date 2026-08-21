using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Haelt fest, dass langer Text in den Schritt-Ansichten umbricht statt sich zu einer
    /// einzigen langen Zeile zu strecken.
    /// <para>
    /// Hintergrund: eine <see cref="Viewbox"/> misst ihr Kind mit unbegrenzter Breite. Ein
    /// <c>TextBlock</c> mit <c>TextWrapping="Wrap"</c> hat darin nie einen Grund umzubrechen -
    /// er misst sich als eine Zeile, und die Viewbox skaliert genau diese Zeile herunter.
    /// Gemessen am 21.08.2026: 2835 px breit, Faktor 0,282, effektiv 4,5 pt statt 22,7 pt.
    /// Erst eine gesetzte <c>MaxWidth</c> loest das.
    /// </para>
    /// </summary>
    [TestClass]
    public class StepTextWrappingUnitTests
    {
        /// <summary>Die Entwurfsbreite, die auch MultipleChoiceStepView benutzt.</summary>
        private const double DesignWidth = 900;

        private static readonly string LangerText = string.Join(" ",
            Enumerable.Repeat("Diesertextistabsichtlichlang", 40));

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
                throw failure is AssertFailedException ? failure
                    : new AssertFailedException($"Die Ansicht liess sich nicht aufbauen: {failure.Message}", failure);
        }

        /// <summary>Sucht rekursiv alle TextBlocks im aufgebauten Baum.</summary>
        private static IEnumerable<TextBlock> TextBlocksIn(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is TextBlock tb)
                    yield return tb;

                foreach (var nested in TextBlocksIn(child))
                    yield return nested;
            }
        }

        private static QuestionStepViewContext ContextFor(QuestionBase question, string stepText)
        {
            var owner = new CurrentQuestionViewModel
            {
                Coordinate = new GameGridCoordinate { QuestionBase = question },
            };

            return new QuestionStepViewContext
            {
                Owner = owner,
                IsMasterView = false,
                Step = new QuestionStepResource { Id = Guid.NewGuid(), StepText = stepText },
            };
        }

        private static void AssertWraps(FrameworkElement view, string expectedText)
        {
            view.Measure(new Size(1200, 700));
            view.Arrange(new Rect(0, 0, 1200, 700));
            view.UpdateLayout();

            var tb = TextBlocksIn(view).FirstOrDefault(t => t.Text == expectedText);

            Assert.IsNotNull(tb, "Der Schritt-Text wurde im aufgebauten Baum nicht gefunden.");
            Assert.AreEqual(TextWrapping.Wrap, tb.TextWrapping);

            Assert.IsTrue(tb.DesiredSize.Width <= DesignWidth + 1,
                $"Der Text ist {tb.DesiredSize.Width:F0} px breit geworden - er bricht also nicht um. "
                + $"Erwartet waren hoechstens {DesignWidth} px. Fehlt eine MaxWidth?");

            Assert.IsTrue(tb.DesiredSize.Height > 40,
                "Bei diesem Text muessen mehrere Zeilen entstehen; eine einzige Zeile heisst, "
                + "der Umbruch greift nicht.");
        }

        [TestMethod]
        public void DefaultStepView_WrapsLongText()
        {
            OnUiThread(() =>
            {
                var context = ContextFor(new DefaultQuestion(), LangerText);
                AssertWraps(new DefaultStepView { DataContext = context }, LangerText);
            });
        }

        [TestMethod]
        public void AppreciateStepView_WrapsLongText()
        {
            OnUiThread(() =>
            {
                var question = new AppreciateQestion
                {
                    ValueKind = AppreciateValueKind.Number,
                    Unit = AppreciateUnit.Stueck,
                    ExpectedValue = 100,
                };

                AssertWraps(new AppreciateStepView { DataContext = ContextFor(question, LangerText) },
                    LangerText);
            });
        }

        /// <summary>
        /// Die Rasterkoepfe standen faelschlich unter Verdacht. Sie sitzen in Spalten mit
        /// absoluter Breite (<c>MatrixGridBehavior</c> setzt <c>GridLength(cellW)</c>), sind
        /// dadurch begrenzt und brechen um - auch ohne MaxWidth. Gemessen: 120 px.
        /// </summary>
        [TestMethod]
        public void AGridCellWithAbsoluteWidthBoundsItsTextEvenInsideAViewbox()
        {
            OnUiThread(() =>
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });

                var tb = new TextBlock { Text = LangerText, TextWrapping = TextWrapping.Wrap };
                grid.Children.Add(tb);

                var box = new Viewbox { Child = grid, Stretch = Stretch.Uniform };
                box.Measure(new Size(800, 400));
                box.Arrange(new Rect(0, 0, 800, 400));
                box.UpdateLayout();

                Assert.AreEqual(120, tb.ActualWidth, 0.5,
                    "Eine absolute Spaltenbreite begrenzt den Text auch innerhalb einer Viewbox.");
            });
        }
    }
}
