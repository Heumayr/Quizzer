using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Speichern einer Frage samt Schritten. Ohne das muesste der Editor die Frage weiterhin
    /// vorab speichern, bevor er den Schritt-Dialog oeffnet.
    /// </summary>
    [TestClass]
    public class QuestionBasesControllerStepsUnitTests
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

        private QuestionBase NewQuestion(params string[] stepNames)
        {
            var question = Factory.CreateNewQuestion(QuestionType.Default);
            question.Id = Guid.NewGuid();
            question.Designation = $"Frage-{Guid.NewGuid():N}";
            question.DesignationShort = "F";
            question.CategoryId = category.Id;

            var sequence = 10;

            foreach (var name in stepNames)
            {
                question.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    Designation = name,
                    StepText = name,
                    SequenceNumber = sequence,
                });

                sequence += 10;
            }

            createdQuestions.Add(question.Id);
            return question;
        }

        private static async Task<QuestionBase?> ReloadAsync(Guid id)
        {
            using var ctrl = new QuestionBasesController();
            return await ctrl.GetAsync(id);
        }

        [TestMethod]
        public async Task SaveWithSteps_PersistsANewQuestionAndItsSteps()
        {
            var question = NewQuestion("Hinweis 1", "Hinweis 2");

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(question);
                await ctrl.SaveChangesAsync();
            }

            var loaded = await ReloadAsync(question.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.Steps.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Hinweis 1", "Hinweis 2" },
                loaded.Steps.Select(s => s.Designation).ToArray());
        }

        [TestMethod]
        public async Task SaveWithSteps_UpdatesAnExistingStep()
        {
            var question = NewQuestion("alter Text");

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(question);
                await ctrl.SaveChangesAsync();
            }

            var loaded = await ReloadAsync(question.Id);
            loaded!.Steps[0].StepText = "neuer Text";

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(loaded);
                await ctrl.SaveChangesAsync();
            }

            var again = await ReloadAsync(question.Id);

            Assert.AreEqual(1, again!.Steps.Count, "Kein Doppelgaenger beim zweiten Speichern.");
            Assert.AreEqual("neuer Text", again.Steps[0].StepText);
        }

        [TestMethod]
        public async Task SaveWithSteps_RemovesStepsThatAreNoLongerThere()
        {
            var question = NewQuestion("bleibt", "faellt weg");

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(question);
                await ctrl.SaveChangesAsync();
            }

            var loaded = await ReloadAsync(question.Id);
            loaded!.Steps.RemoveAll(s => s.Designation == "faellt weg");

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(loaded);
                await ctrl.SaveChangesAsync();
            }

            var again = await ReloadAsync(question.Id);

            Assert.AreEqual(1, again!.Steps.Count);
            Assert.AreEqual("bleibt", again.Steps[0].Designation);
        }

        [TestMethod]
        public async Task SaveWithSteps_WithoutSteps_LeavesTheQuestionEmpty()
        {
            var question = NewQuestion();

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(question);
                await ctrl.SaveChangesAsync();
            }

            var loaded = await ReloadAsync(question.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.Steps.Count);
        }

        /// <summary>
        /// Der Grund, warum das eine eigene Methode ist und nicht in UpsertAsync eingebaut wurde:
        /// die Fragenliste laedt ueber GetAllAsync ohne Schritte. Ein Gleichzug dort wuerde jeder
        /// Frage saemtliche Schritte loeschen.
        /// </summary>
        [TestMethod]
        public async Task Upsert_StillLeavesStepsAlone()
        {
            var question = NewQuestion("Hinweis 1");

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.SaveWithStepsAsync(question);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                var withoutSteps = (await ctrl.GetAllAsync()).First(q => q.Id == question.Id);
                Assert.AreEqual(0, withoutSteps.Steps.Count, "GetAllAsync laedt keine Schritte.");

                await ctrl.UpsertAsync(withoutSteps);
                await ctrl.SaveChangesAsync();
            }

            var loaded = await ReloadAsync(question.Id);

            Assert.AreEqual(1, loaded!.Steps.Count,
                "UpsertAsync darf die Schritte weder schreiben noch loeschen.");
        }
    }
}
