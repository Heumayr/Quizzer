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
