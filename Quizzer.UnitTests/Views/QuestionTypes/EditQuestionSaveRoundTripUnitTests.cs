using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.QuestionTypes;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Der Weg, der frueher das Vorab-Speichern gebraucht hat: eine neue Frage anlegen,
    /// Schritte hinzufuegen, speichern. Hier gegen die echte Testdatenbank, weil genau hier
    /// stille Verluste entstuenden.
    /// </summary>
    [TestClass]
    public class EditQuestionSaveRoundTripUnitTests
    {
        private Category category = null!;
        private readonly List<Guid> createdQuestions = new();

        [TestInitialize]
        public async Task CreateCategory()
        {
            using var ctrl = new CategoriesController();
            category = new Category { Id = Guid.NewGuid(), Designation = $"Kat-{Guid.NewGuid():N}" };
            await ctrl.InsertAsync(category);
            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            using (var ctrl = new QuestionBasesController())
            {
                foreach (var id in createdQuestions)
                    await ctrl.DeleteAsync(id);

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(category.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        private EditQuestionViewModel NewEditor()
        {
            var question = Factory.CreateNewQuestion(QuestionType.Default);
            question.Id = Guid.NewGuid();
            question.Designation = $"Frage-{Guid.NewGuid():N}";
            question.DesignationShort = "F";
            question.CategoryId = category.Id;

            createdQuestions.Add(question.Id);

            return new EditQuestionViewModel { Question = question };
        }

        private static QuestionStepResource Step(string name, int sequence)
            => new() { Id = Guid.NewGuid(), Designation = name, StepText = name, SequenceNumber = sequence };

        private static async Task<int> StepCountInDatabaseAsync(Guid questionId)
        {
            using var ctrl = new QuestionBasesController();
            var loaded = await ctrl.GetAsync(questionId);
            return loaded?.Steps.Count ?? -1;
        }

        [TestMethod]
        public async Task ANewQuestionWithStepsIsStoredCompletely()
        {
            var vm = NewEditor();
            vm.Question!.Steps.Add(Step("Hinweis 1", 10));
            vm.Question.Steps.Add(Step("Hinweis 2", 20));

            await vm.VMSaveAsync();

            Assert.AreEqual(2, await StepCountInDatabaseAsync(vm.Question.Id),
                "Genau hier gingen die Schritte frueher verloren, wenn nicht vorab gespeichert wurde.");
        }

        /// <summary>
        /// Der eigentliche Gewinn: bis zum Speichern steht nichts im Bestand.
        /// </summary>
        [TestMethod]
        public async Task NothingIsWrittenBeforeSaving()
        {
            var vm = NewEditor();
            vm.Question!.Steps.Add(Step("Hinweis 1", 10));

            using var ctrl = new QuestionBasesController();
            var loaded = await ctrl.GetAsync(vm.Question.Id);

            Assert.IsNull(loaded,
                "Vor dem Speichern darf die halbfertige Frage nicht im Bestand stehen.");
        }

        [TestMethod]
        public async Task RemovingAStepAndSavingRemovesItFromTheDatabase()
        {
            var vm = NewEditor();
            vm.Question!.Steps.Add(Step("bleibt", 10));
            vm.Question.Steps.Add(Step("faellt weg", 20));
            await vm.VMSaveAsync();
            Assert.AreEqual(2, await StepCountInDatabaseAsync(vm.Question.Id));

            var drop = vm.Question.Steps.First(s => s.Designation == "faellt weg");
            vm.SelectedSteps = new List<QuestionStepResource> { drop };
            vm.RemoveStepCommnad.Execute(null);
            await vm.VMSaveAsync();

            Assert.AreEqual(1, await StepCountInDatabaseAsync(vm.Question.Id));
        }

        [TestMethod]
        public async Task SavingTwiceDoesNotDuplicateSteps()
        {
            var vm = NewEditor();
            vm.Question!.Steps.Add(Step("Hinweis", 10));

            await vm.VMSaveAsync();
            await vm.VMSaveAsync();

            Assert.AreEqual(1, await StepCountInDatabaseAsync(vm.Question.Id));
        }

        [TestMethod]
        public async Task EditingAStepTextIsStored()
        {
            var vm = NewEditor();
            var step = Step("alter Text", 10);
            vm.Question!.Steps.Add(step);
            await vm.VMSaveAsync();

            step.StepText = "neuer Text";
            await vm.VMSaveAsync();

            using var ctrl = new QuestionBasesController();
            var loaded = await ctrl.GetAsync(vm.Question.Id);

            Assert.AreEqual("neuer Text", loaded!.Steps.Single().StepText);
        }
    }
}
