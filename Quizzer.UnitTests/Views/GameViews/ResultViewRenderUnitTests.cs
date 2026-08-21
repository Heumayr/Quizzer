using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Baut die Ansichten des Spielablaufs wirklich auf. Ein gruener Build sagt nur, dass das
    /// XAML uebersetzt - eine Bindung ins Leere oder eine fehlende Ressource zeigt sich erst
    /// beim Aufbau.
    /// </summary>
    [TestClass]
    public class ResultViewRenderUnitTests
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

        private static void Build(FrameworkElement element)
        {
            element.Measure(new Size(800, 600));
            element.Arrange(new Rect(0, 0, 800, 600));
            element.UpdateLayout();
        }

        [TestMethod]
        public void ThePlayerTileBuildsWithAGuess()
        {
            OnUiThread(() =>
            {
                var player = new Player { Id = Guid.NewGuid(), Designation = "Anna" };
                var context = new PlayerResultContext
                {
                    Player = player,
                    Result = new QuestionResult { Id = Guid.NewGuid(), PlayerId = player.Id },
                };

                context.InputResult = new LocalBuzzer.Service.Base.States.BuzzerInputState.InputResult
                {
                    PlayerId = player.Id,
                    Player = player,
                    Value = "3750",
                };

                var view = new UcPlayerResultView { DataContext = context };
                Build(view);

                Assert.AreEqual(Visibility.Visible, context.ShowAppreciateGuess);
            });
        }

        [TestMethod]
        public void ThePlayerTileBuildsWithoutAGuess()
        {
            OnUiThread(() =>
            {
                var player = new Player { Id = Guid.NewGuid(), Designation = "Bert" };
                var context = new PlayerResultContext
                {
                    Player = player,
                    Result = new QuestionResult { Id = Guid.NewGuid(), PlayerId = player.Id },
                };

                var view = new UcPlayerResultView { DataContext = context };
                Build(view);

                Assert.AreEqual(Visibility.Collapsed, context.ShowAppreciateGuess,
                    "Ohne Tipp bleibt die Kachel so wie bei jeder anderen Frage.");
            });
        }

        [TestMethod]
        public void TheAppreciateStepViewBuilds()
        {
            OnUiThread(() =>
            {
                var question = new AppreciateQestion
                {
                    ValueKind = AppreciateValueKind.Length,
                    Unit = AppreciateUnit.Meter,
                    ExpectedValue = 3798,
                };

                var owner = new CurrentQuestionViewModel
                {
                    Coordinate = new GameGridCoordinate { QuestionBase = question },
                };

                var context = new Quizzer.Views.GameViews.QuestionViews.QuestionStepViewContext
                {
                    Owner = owner,
                    IsMasterView = true,
                    Step = new QuestionStepResource { Id = Guid.NewGuid(), StepText = "Wie hoch?" },
                };

                var view = new AppreciateStepView { DataContext = context };
                Build(view);

                StringAssert.Contains(context.AppreciateExpectedText, "3798");
            });
        }
    }
}
