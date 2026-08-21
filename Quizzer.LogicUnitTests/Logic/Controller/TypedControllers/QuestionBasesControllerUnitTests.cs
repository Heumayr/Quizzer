using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Fragen gegen die echte Testdatenbank. Hier zaehlt vor allem, dass die
    /// Table-per-Type-Vererbung den konkreten Typ ueber einen Neustart hinweg behaelt.
    /// </summary>
    [TestClass]
    public class QuestionBasesControllerUnitTests
    {
        private Category category = null!;

        [TestInitialize]
        public async Task CreateCategory()
        {
            using var ctrl = new CategoriesController();
            category = new Category { Id = Guid.NewGuid(), Designation = $"Kat-{Guid.NewGuid():N}" };
            await ctrl.InsertAsync(category);
            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task RemoveCategory()
        {
            using var ctrl = new CategoriesController();
            await ctrl.DeleteAsync(category.Id);
            await ctrl.SaveChangesAsync();
        }

        private QuestionBase NewQuestion(QuestionType type)
        {
            var question = Quizzer.DataModels.Helpers.Factory.CreateNewQuestion(type);
            question.Id = Guid.NewGuid();
            question.Designation = $"Frage-{Guid.NewGuid():N}";
            question.DesignationShort = "F";
            question.CategoryId = category.Id;
            return question;
        }

        [TestMethod]
        [DataRow(QuestionType.Default, typeof(DefaultQuestion))]
        [DataRow(QuestionType.MultipleChoice, typeof(MultipleChoiceQuestion))]
        [DataRow(QuestionType.Properties, typeof(PropertiesQuestion))]
        [DataRow(QuestionType.Appreciate, typeof(AppreciateQestion))]
        public async Task Upsert_ThenGet_KeepsTheConcreteType(QuestionType type, Type expected)
        {
            var question = NewQuestion(type);

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(question);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                var loaded = await ctrl.GetAsync(question.Id);

                Assert.IsNotNull(loaded);
                Assert.IsInstanceOfType(loaded, expected);
                Assert.AreEqual(type, loaded.Typ);

                await ctrl.DeleteAsync(question.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        [TestMethod]
        public async Task Upsert_KeepsTheTypeDefaultsFromTheConstructor()
        {
            var question = NewQuestion(QuestionType.MultipleChoice);

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(question);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                var loaded = await ctrl.GetAsync(question.Id);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(BuzzerControlsLayout.KeySelect, loaded.BuzzerControlsLayout);
                Assert.AreEqual(StepDisplayLayoutMode.Grid, loaded.StepDisplayLayoutMode);
                Assert.IsTrue(loaded.UseRandomSequenceOnNoneFinishSteps);

                await ctrl.DeleteAsync(question.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        [TestMethod]
        public async Task Get_LoadsTheCategory()
        {
            var question = NewQuestion(QuestionType.Default);

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(question);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                var loaded = await ctrl.GetAsync(question.Id);

                Assert.IsNotNull(loaded?.Category);
                Assert.AreEqual(category.Id, loaded.Category.Id);

                await ctrl.DeleteAsync(question.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Haelt den HEUTIGEN Stand fest: CopyQuestionBaseValuesTo leert die Schrittliste, und
        /// GenericController.CloneForEf schreibt genau diesen Klon. Schritte werden ueber
        /// UpsertAsync also NICHT gespeichert - deshalb speichert der Editor die Frage vorab,
        /// bevor er den Schritt-Dialog oeffnet. Wird in Phase 2 umgedreht.
        /// </summary>
        [TestMethod]
        public async Task Upsert_DoesNotPersistSteps_KnownShortcoming()
        {
            var question = NewQuestion(QuestionType.Default);
            question.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                Designation = "Schritt eins",
                SequenceNumber = 10,
            });

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(question);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                var loaded = await ctrl.GetAsync(question.Id);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(0, loaded.Steps.Count,
                    "Sobald die Schritt-Kaskade steht, muss hier 1 stehen.");

                await ctrl.DeleteAsync(question.Id);
                await ctrl.SaveChangesAsync();
            }
        }
    }
}
