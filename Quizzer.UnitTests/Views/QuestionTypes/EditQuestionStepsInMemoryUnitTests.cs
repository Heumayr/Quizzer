using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.QuestionTypes;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Schritte werden im Editor gehalten und erst beim Speichern geschrieben.
    /// <para>
    /// Frueher schrieb der Schritt-Dialog jeden Schritt sofort in die Datenbank. Weil dafuer
    /// die Frage schon existieren musste, hat der Frage-Editor sie vor jedem Schritt still
    /// vorab gespeichert - eine halbfertige Frage stand danach im Bestand, auch wenn der
    /// Spielleiter abgebrochen hat.
    /// </para>
    /// </summary>
    [TestClass]
    public class EditQuestionStepsInMemoryUnitTests
    {
        private static EditQuestionViewModel EditorFor(QuestionType typ = QuestionType.Default)
        {
            var question = Factory.CreateNewQuestion(typ);
            question.Designation = "Testfrage";
            question.DesignationShort = "TF";
            question.CategoryId = Guid.NewGuid();

            return new EditQuestionViewModel { Question = question };
        }

        private static QuestionStepResource Step(string name, int sequence)
            => new() { Id = Guid.NewGuid(), Designation = name, StepText = name, SequenceNumber = sequence };

        [TestMethod]
        public void RemovingAStepOnlyTouchesTheQuestionInMemory()
        {
            var vm = EditorFor();
            var keep = Step("bleibt", 10);
            var drop = Step("faellt weg", 20);
            vm.Question!.Steps.Add(keep);
            vm.Question.Steps.Add(drop);

            vm.SelectedSteps = new List<QuestionStepResource> { drop };
            vm.RemoveStepCommnad.Execute(null);

            CollectionAssert.AreEqual(
                new[] { "bleibt" },
                vm.Question.Steps.Select(s => s.Designation).ToArray());
        }

        [TestMethod]
        public void RemovingAStepRefreshesTheDisplayedList()
        {
            var vm = EditorFor();
            vm.Question!.Steps.Add(Step("a", 10));
            vm.Question.Steps.Add(Step("b", 20));

            vm.SelectedSteps = new List<QuestionStepResource>(vm.Question.Steps.Take(1));
            vm.RemoveStepCommnad.Execute(null);

            Assert.AreEqual(1, vm.Steps.Count);
        }

        /// <summary>
        /// Eine Multiple-Choice-Frage braucht zwei Schritte. Nimmt man einen weg, muss die
        /// Pruefung sofort anschlagen - sonst faellt es erst beim Speichern auf.
        /// </summary>
        [TestMethod]
        public void RemovingAStepRevalidatesImmediately()
        {
            var vm = EditorFor(QuestionType.MultipleChoice);
            vm.Question!.Steps.Add(Step("Wien", 10));
            vm.Question.Steps.Add(Step("Graz", 20));
            vm.Question.Steps[0].IsResult = true;
            vm.Question.BuzzerMaxAllowedKeySelect = 1;
            vm.Revalidate();
            Assert.IsTrue(vm.CanSave);

            vm.SelectedSteps = new List<QuestionStepResource> { vm.Question.Steps[1] };
            vm.RemoveStepCommnad.Execute(null);

            Assert.IsFalse(vm.CanSave, "Zu wenige Optionen muessen sofort auffallen.");
        }

        [TestMethod]
        public void RemovingNothingChangesNothing()
        {
            var vm = EditorFor();
            vm.Question!.Steps.Add(Step("a", 10));

            vm.SelectedSteps = new List<QuestionStepResource>();
            vm.RemoveStepCommnad.Execute(null);

            Assert.AreEqual(1, vm.Question.Steps.Count);
        }
    }
}
