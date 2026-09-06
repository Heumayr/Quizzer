using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using Quizzer.Views.QuestionTypes;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Die Typwahl vor dem Anlegen. Sie ersetzt die unbeschriftete Auswahlliste in der
    /// Fragenliste und die fest verdrahtete Standardfrage in der Fragenauswahl.
    /// </summary>
    [TestClass]
    public class NewQuestionViewModelUnitTests
    {
        [TestMethod]
        public void EveryQuestionTypeIsOfferedWithNameAndExplanation()
        {
            var vm = new NewQuestionViewModel();

            Assert.AreEqual(QuestionTypeProfiles.All.Count, vm.Profiles.Count);
            Assert.IsTrue(vm.Profiles.All(p => !string.IsNullOrWhiteSpace(p.DisplayName)));
            Assert.IsTrue(vm.Profiles.All(p => !string.IsNullOrWhiteSpace(p.HelpText)));
        }

        /// <summary>
        /// Frueher stand die Auswahlliste stumm auf Standardfrage - wer sie uebersah, legte
        /// ungewollt eine Standardfrage an.
        /// </summary>
        [TestMethod]
        public void NothingIsPreselected()
        {
            var vm = new NewQuestionViewModel();

            Assert.IsNull(vm.SelectedProfile);
            Assert.IsFalse(vm.CanCreate);
            Assert.IsFalse(vm.CreateCommand.CanExecute(null));
        }

        [TestMethod]
        public void ChoosingATypeEnablesCreating()
        {
            var vm = new NewQuestionViewModel
            {
                SelectedProfile = QuestionTypeProfiles.For(QuestionType.MultipleChoice),
            };

            Assert.IsTrue(vm.CanCreate);
            Assert.IsTrue(vm.CreateCommand.CanExecute(null));
        }

        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.MultipleChoice)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void CreatingReturnsTheChosenType(QuestionType typ)
        {
            var vm = new NewQuestionViewModel { SelectedProfile = QuestionTypeProfiles.For(typ) };

            vm.CreateCommand.Execute(null);

            Assert.AreEqual(typ, vm.ChosenType);
        }

        [TestMethod]
        public void CancellingReturnsNothing()
        {
            var vm = new NewQuestionViewModel
            {
                SelectedProfile = QuestionTypeProfiles.For(QuestionType.Appreciate),
            };

            vm.CancelCommand.Execute(null);

            Assert.IsNull(vm.ChosenType, "Abgebrochen heisst: es wird keine Frage angelegt.");
        }

        [TestMethod]
        public void TheWindowBuilds()
        {
            // Laeuft auf dem gemeinsamen Oberflaechen-Thread; ein eigener wuerde beim
            // Herunterfahren den prozessweiten Application.Current mitnehmen (siehe UiTestHost).
            UiTestHost.Run(() =>
            {
                var view = new NewQuestionView();
                view.Measure(new Size(620, 520));
                view.Arrange(new Rect(0, 0, 620, 520));
                view.UpdateLayout();

                Assert.IsNotNull(view.ViewModel);
                // Bewusst gegen die Profilliste und nicht gegen eine Zahl: sie war bis
                // 2026-09-06 eine 4, und mit der Aufdeckfrage waere sie still falsch geworden.
                Assert.AreEqual(QuestionTypeProfiles.All.Count, view.ViewModel.Profiles.Count,
                    "Die Auswahl bietet nicht jeden Fragetyp an.");
            });
        }
    }
}
