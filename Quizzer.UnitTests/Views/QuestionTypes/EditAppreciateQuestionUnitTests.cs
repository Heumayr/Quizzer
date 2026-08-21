using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Questions;
using Quizzer.Views.QuestionTypes;
using System.Windows;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Der Schaetzfrage-Teil des Editors: Art des Werts, Einheit und Sollwert.
    /// </summary>
    [TestClass]
    public class EditAppreciateQuestionUnitTests
    {
        private static EditQuestionViewModel Editor()
        {
            var question = Factory.CreateNewQuestion(QuestionType.Appreciate);
            question.Designation = "Hoehe des Grossglockners";
            question.DesignationShort = "GG";
            question.CategoryId = Guid.NewGuid();

            return new EditQuestionViewModel { Question = question };
        }

        [TestMethod]
        public void TheEditorOffersTheAppreciateFields()
        {
            var vm = Editor();

            Assert.AreEqual(Visibility.Visible, vm.ExpectedValueVisibility);
            Assert.IsNotNull(vm.Appreciate);
        }

        [TestMethod]
        public void ByDefault_ANumberIsExpected()
        {
            var vm = Editor();

            Assert.AreEqual(AppreciateValueKind.Number, vm.AppreciateValueKind);
            Assert.AreEqual(Visibility.Visible, vm.ExpectedNumberVisibility);
            Assert.AreEqual(Visibility.Collapsed, vm.ExpectedDateVisibility);
        }

        [TestMethod]
        public void ChoosingDate_SwapsTheNumberFieldForADatePicker()
        {
            var vm = Editor();

            vm.AppreciateValueKind = AppreciateValueKind.Date;

            Assert.IsTrue(vm.ExpectedValueIsDate);
            Assert.AreEqual(Visibility.Collapsed, vm.ExpectedNumberVisibility);
            Assert.AreEqual(Visibility.Visible, vm.ExpectedDateVisibility);
        }

        [TestMethod]
        public void ChoosingAKind_OffersOnlyItsUnits()
        {
            var vm = Editor();

            vm.AppreciateValueKind = AppreciateValueKind.Length;

            var offered = vm.AppreciateUnitsForKind.Select(u => u.Unit).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { AppreciateUnit.Kilometer, AppreciateUnit.Meter,
                        AppreciateUnit.Zentimeter, AppreciateUnit.Millimeter },
                offered);
        }

        /// <summary>
        /// Sonst bliebe nach einem Wechsel eine Einheit stehen, die zur neuen Art nicht passt -
        /// der Nutzer liefe in eine Beanstandung, die er nicht verursacht hat.
        /// </summary>
        [TestMethod]
        public void SwitchingTheKind_PullsTheUnitAlong()
        {
            var vm = Editor();
            vm.AppreciateValueKind = AppreciateValueKind.Mass;
            Assert.IsTrue(AppreciateUnits.Matches(AppreciateValueKind.Mass, vm.Appreciate!.Unit));

            vm.AppreciateValueKind = AppreciateValueKind.Volume;

            Assert.IsTrue(AppreciateUnits.Matches(AppreciateValueKind.Volume, vm.Appreciate.Unit),
                "Nach dem Wechsel muss die Einheit zur neuen Art passen.");
        }

        [TestMethod]
        public void ADateQuestionWithoutADate_CannotBeSaved()
        {
            var vm = Editor();
            vm.AppreciateValueKind = AppreciateValueKind.Date;
            vm.ExpectedDate = null;
            vm.Revalidate();

            Assert.IsFalse(vm.CanSave);
            Assert.IsTrue(vm.Issues.Any(i => i.Code == QuestionValidator.ExpectedDateMissing));
        }

        [TestMethod]
        public void ADateQuestionWithADate_CanBeSaved()
        {
            var vm = Editor();
            vm.AppreciateValueKind = AppreciateValueKind.Date;
            vm.ExpectedDate = new DateTime(1969, 7, 20);
            vm.Revalidate();

            Assert.IsTrue(vm.CanSave,
                "Unerwartet beanstandet: " + string.Join(", ", vm.Issues.Select(i => i.Code)));
        }

        [TestMethod]
        public void AnExpectedValueOfZero_IsOnlyAWarning()
        {
            var vm = Editor();
            vm.ExpectedValue = 0;
            vm.Revalidate();

            Assert.IsTrue(vm.Issues.Any(i => i.Code == QuestionValidator.ExpectedValueMissing
                                             && !i.IsError));
            Assert.IsTrue(vm.CanSave);
        }

        [TestMethod]
        public void TheHintExplainsWhatThePlayersSeeAndHowItIsScored()
        {
            var vm = Editor();
            vm.AppreciateValueKind = AppreciateValueKind.Length;
            vm.AppreciateUnit = AppreciateUnits.Info(AppreciateUnit.Meter);
            vm.ExpectedValue = 3798;

            StringAssert.Contains(vm.AppreciateHintText, "3798");
            StringAssert.Contains(vm.AppreciateHintText, "naechsten");
        }

        [TestMethod]
        public void OtherQuestionTypes_HaveNoAppreciateFields()
        {
            var question = Factory.CreateNewQuestion(QuestionType.Default);
            question.CategoryId = Guid.NewGuid();
            var vm = new EditQuestionViewModel { Question = question };

            Assert.IsNull(vm.Appreciate);
            Assert.AreEqual(Visibility.Collapsed, vm.ExpectedValueVisibility);
            Assert.AreEqual(Visibility.Collapsed, vm.ExpectedNumberVisibility);
        }
    }
}
