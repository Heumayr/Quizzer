using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.Views.QuestionTypes;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Baut das Editorfenster wirklich auf. Ein gruener Build sagt nur, dass das XAML
    /// uebersetzt - ob eine Bindung ins Leere zeigt oder eine Ressource fehlt, zeigt sich
    /// erst beim Aufbau. Genau daran ist frueher der Aufruf der Anwendung gescheitert,
    /// obwohl alles gebaut hat.
    /// </summary>
    [TestClass]
    public class EditQuestionsViewRenderUnitTests
    {
        /// <summary>
        /// Fuehrt die Aktion auf einem STA-Thread mit eigener Application aus. WPF verlangt
        /// beides; MSTest liefert von sich aus keinen STA-Thread.
        /// </summary>
        private static void OnUiThread(Action action)
        {
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        // Die echte App.xaml laden: dort stehen Palette und Stile, auf die die
                        // Ansichten ueber StaticResource zugreifen. Eine nackte Application
                        // haette sie nicht - der Aufbau scheitert dann an der ersten Farbe.
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
                    $"Das Editorfenster liess sich nicht aufbauen: {failure.Message}", failure);
        }

        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.MultipleChoice)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void TheEditorWindow_BuildsForEveryQuestionType(QuestionType typ)
        {
            OnUiThread(() =>
            {
                var view = new EditQuestionsView();

                var vm = (EditQuestionViewModel)view.DataContext;
                var question = Factory.CreateNewQuestion(typ);
                question.Designation = "Testfrage";
                question.CategoryId = Guid.NewGuid();
                vm.Question = question;

                // Erzwingt Aufbau und Bindungsaufloesung, ohne das Fenster zu zeigen.
                view.Measure(new Size(900, 720));
                view.Arrange(new Rect(0, 0, 900, 720));
                view.UpdateLayout();

                Assert.AreEqual(typ, vm.Question!.Typ);
            });
        }
    }
}
