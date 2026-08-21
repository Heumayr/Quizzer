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

            CollectionAssert.AreEqual(
                AppreciateUnits.For(AppreciateValueKind.Length).Select(u => u.Unit).ToArray(),
                offered,
                "Die Auswahlliste zeigt genau die Einheiten dieser Groesse, in der Reihenfolge "
                + "der Tabelle - die erste ist die Vorauswahl.");

            Assert.IsFalse(offered.Contains(AppreciateUnit.Kilogramm),
                "Einheiten anderer Groessen duerfen nicht auftauchen.");
        }

        /// <summary>
        /// Beim Wechsel der Art wird immer die erste Einheit der neuen Liste eingesetzt.
        /// </summary>
        [TestMethod]
        [DataRow(AppreciateValueKind.Length, AppreciateUnit.Meter)]
        [DataRow(AppreciateValueKind.Mass, AppreciateUnit.Kilogramm)]
        [DataRow(AppreciateValueKind.Volume, AppreciateUnit.Liter)]
        [DataRow(AppreciateValueKind.Duration, AppreciateUnit.Jahre)]
        [DataRow(AppreciateValueKind.Area, AppreciateUnit.Quadratmeter)]
        [DataRow(AppreciateValueKind.Temperature, AppreciateUnit.GradCelsius)]
        [DataRow(AppreciateValueKind.Speed, AppreciateUnit.KilometerProStunde)]
        [DataRow(AppreciateValueKind.Power, AppreciateUnit.Pferdestaerken)]
        [DataRow(AppreciateValueKind.Energy, AppreciateUnit.Kilokalorien)]
        [DataRow(AppreciateValueKind.DataVolume, AppreciateUnit.Gigabyte)]
        public void SwitchingTheKind_TakesTheFirstUnitOfTheNewList(
            AppreciateValueKind kind, AppreciateUnit expected)
        {
            var vm = Editor();

            vm.AppreciateValueKind = kind;

            Assert.AreEqual(expected, vm.Appreciate!.Unit);
            Assert.AreEqual(expected, vm.AppreciateUnitsForKind[0].Unit,
                "Die Vorauswahl muss der ersten Zeile der Auswahlliste entsprechen.");
        }

        /// <summary>
        /// Auch dann, wenn vorher von Hand eine andere Einheit gewaehlt wurde: ein Wechsel der
        /// Art setzt die Einheit zurueck, statt irgendetwas zu behalten.
        /// </summary>
        [TestMethod]
        public void SwitchingTheKind_DiscardsAHandPickedUnit()
        {
            var vm = Editor();
            vm.AppreciateValueKind = AppreciateValueKind.Length;
            vm.AppreciateUnit = AppreciateUnits.Info(AppreciateUnit.Lichtjahr);
            Assert.AreEqual(AppreciateUnit.Lichtjahr, vm.Appreciate!.Unit);

            vm.AppreciateValueKind = AppreciateValueKind.Mass;

            Assert.AreEqual(AppreciateUnit.Kilogramm, vm.Appreciate.Unit);
        }

        [TestMethod]
        public void SwitchingBackAndForth_LandsOnTheFirstUnitAgain()
        {
            var vm = Editor();
            vm.AppreciateValueKind = AppreciateValueKind.Length;
            vm.AppreciateUnit = AppreciateUnits.Info(AppreciateUnit.Zoll);

            vm.AppreciateValueKind = AppreciateValueKind.Volume;
            vm.AppreciateValueKind = AppreciateValueKind.Length;

            Assert.AreEqual(AppreciateUnit.Meter, vm.Appreciate!.Unit,
                "Zurueck heisst nicht: die zuletzt gewaehlte Einheit wieder herstellen.");
        }

        [TestMethod]
        public void TheUnitListFollowsTheKind()
        {
            var vm = Editor();

            vm.AppreciateValueKind = AppreciateValueKind.Temperature;

            CollectionAssert.AreEqual(
                new[] { AppreciateUnit.GradCelsius, AppreciateUnit.Kelvin, AppreciateUnit.GradFahrenheit },
                vm.AppreciateUnitsForKind.Select(u => u.Unit).ToArray());
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
