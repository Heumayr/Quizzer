using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.Views.QuestionTypes;
using System.Windows;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Der Frage-Editor: zeigt er pro Fragetyp das Richtige, und haelt er das Speichern an,
    /// solange die Frage nicht spielbar ist.
    /// </summary>
    [TestClass]
    public class EditQuestionViewModelUnitTests
    {
        private static EditQuestionViewModel EditorFor(QuestionType typ)
        {
            var question = Factory.CreateNewQuestion(typ);
            question.Designation = "Testfrage";
            question.DesignationShort = "TF";
            question.CategoryId = Guid.NewGuid();

            return new EditQuestionViewModel { Question = question };
        }

        [TestMethod]
        [DataRow(QuestionType.Default, "Standardfrage")]
        [DataRow(QuestionType.MultipleChoice, "Multiple Choice")]
        [DataRow(QuestionType.Properties, "Eigenschaftsfrage")]
        [DataRow(QuestionType.Appreciate, "Schaetzfrage")]
        public void TheEditor_NamesTheQuestionType(QuestionType typ, string expected)
        {
            var vm = EditorFor(typ);

            Assert.AreEqual(expected, vm.TypeDisplayName,
                "Bis hierher war im Editor nirgends zu sehen, welcher Typ bearbeitet wird.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(vm.TypeHelpText));
        }

        [TestMethod]
        public void MultipleChoice_ShowsTheKeySelectFields()
        {
            var vm = EditorFor(QuestionType.MultipleChoice);

            Assert.AreEqual(Visibility.Visible, vm.MaxAllowedKeySelectVisibility,
                "Ohne dieses Feld ist eine Frage mit mehreren richtigen Antworten nicht anlegbar.");
            Assert.AreEqual(Visibility.Visible, vm.ShowTextOnKeySelectVisibility);
            Assert.AreEqual(Visibility.Visible, vm.RandomSequenceHintVisibility);
        }

        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void OtherTypes_HideTheKeySelectFields(QuestionType typ)
        {
            var vm = EditorFor(typ);

            Assert.AreEqual(Visibility.Collapsed, vm.MaxAllowedKeySelectVisibility);
            Assert.AreEqual(Visibility.Collapsed, vm.ShowTextOnKeySelectVisibility);
        }

        [TestMethod]
        public void Properties_ExplainsTheShrinkingScore()
        {
            var vm = EditorFor(QuestionType.Properties);

            Assert.AreEqual(Visibility.Visible, vm.ProportionalScoreHintVisibility);
            Assert.AreEqual(Visibility.Collapsed, vm.RandomSequenceHintVisibility,
                "Gemischte Hinweise widersprechen dem Sinn der Eigenschaftsfrage.");
        }

        [TestMethod]
        public void Appreciate_AsksForAnExpectedValue()
        {
            var vm = EditorFor(QuestionType.Appreciate);

            Assert.AreEqual(Visibility.Visible, vm.ExpectedValueVisibility);
        }

        [TestMethod]
        public void TheTypeOwnedValues_AreShownInPlainGerman()
        {
            var vm = EditorFor(QuestionType.MultipleChoice);

            StringAssert.Contains(vm.BuzzerLayoutText, "Tastenwahl");
            StringAssert.Contains(vm.StepLayoutText, "Raster");
            StringAssert.Contains(vm.ViewKeyText, "A, B, C");
        }

        [TestMethod]
        public void AQuestionWithoutCategory_CannotBeSaved()
        {
            var vm = EditorFor(QuestionType.Default);
            vm.Question!.CategoryId = Guid.Empty;
            vm.Revalidate();

            Assert.IsFalse(vm.CanSave,
                "Bis hierher lief das in einen rohen DbUpdateException.");
            Assert.IsTrue(vm.Issues.Any(i => i.Code == QuestionValidator.CategoryMissing));
            Assert.AreEqual(Visibility.Visible, vm.IssuesVisibility);
        }

        [TestMethod]
        public void MultipleChoiceWithoutOptions_CannotBeSaved()
        {
            var vm = EditorFor(QuestionType.MultipleChoice);
            vm.Revalidate();

            Assert.IsFalse(vm.CanSave);
            Assert.IsTrue(vm.Issues.Any(i => i.Code == QuestionValidator.TooFewSteps));
        }

        [TestMethod]
        public void AProperMultipleChoiceQuestion_CanBeSaved()
        {
            var vm = EditorFor(QuestionType.MultipleChoice);
            vm.Question!.Steps.Add(new QuestionStepResource
            { Id = Guid.NewGuid(), Designation = "Wien", StepText = "Wien", IsResult = true });
            vm.Question.Steps.Add(new QuestionStepResource
            { Id = Guid.NewGuid(), Designation = "Graz", StepText = "Graz" });
            vm.Question.BuzzerMaxAllowedKeySelect = 1;
            vm.Revalidate();

            Assert.IsTrue(vm.CanSave,
                "Unerwartet beanstandet: " + string.Join(", ", vm.Issues.Select(i => i.Code)));
        }

        [TestMethod]
        public void SaveCommand_IsDisabledWhileAnErrorIsOpen()
        {
            var vm = EditorFor(QuestionType.MultipleChoice);
            vm.Revalidate();

            Assert.IsFalse(vm.SaveCommand.CanExecute(null));
            Assert.IsFalse(vm.SaveAndCloseCommand.CanExecute(null));
        }

        [TestMethod]
        public void AWarningDoesNotBlockSaving()
        {
            var vm = EditorFor(QuestionType.Default);
            vm.Question!.DesignationShort = string.Empty;
            vm.Revalidate();

            Assert.IsTrue(vm.Issues.Any(i => !i.IsError));
            Assert.IsTrue(vm.CanSave);
        }

        [TestMethod]
        public void SettingTheQuestion_DoesNotDropItsCategory()
        {
            var question = Factory.CreateNewQuestion(QuestionType.Default);
            var category = new Category { Id = Guid.NewGuid(), Designation = "Geschichte" };
            question.Category = category;
            question.CategoryId = category.Id;

            var vm = new EditQuestionViewModel { Question = question };

            Assert.AreSame(category, vm.Question!.Category,
                "Der Setter hat die Kategorie frueher auf null gesetzt, weil er auf die noch "
                + "leere Kategorienliste zugegriffen hat.");
        }

        [TestMethod]
        public void CancelCommand_MarksTheResultAsCancelled()
        {
            var vm = EditorFor(QuestionType.Default);

            vm.CancelCommand.Execute(null);

            Assert.AreEqual(EditResultState.Canceled, vm.ResultState);
        }
    }
}
