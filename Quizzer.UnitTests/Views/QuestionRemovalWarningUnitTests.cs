using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Was geschieht, wenn der Spielleiter eine Frage entfernt.
    /// <para>
    /// Bis 2026-09-06 löschte dieser Weg <b>ohne jede Rückfrage</b>. Zwei Folgen hingen daran:
    /// <c>QuestionResult.QuestionBaseId</c> steht auf CASCADE - mit der Frage verschwand ihre
    /// gesamte Spielhistorie; und liegt die Frage in einem Raster, scheitert das Löschen am
    /// NO ACTION von <c>GameGridCoordinate</c>, und es kam ein roher Datenbankfehler.
    /// </para>
    /// </summary>
    [TestClass]
    public class QuestionRemovalWarningUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;

            TestEnvironment.ClearSwallowedExceptions();
            TestEnvironment.ClearDiscardedChanges();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
            TestEnvironment.ClearDiscardedChanges();
        }

        private async Task<QuestionsViewModel> BuildViewModelAsync(QuestionBase frage)
        {
            var vm = new QuestionsViewModel();

            await vm.LoadForTestAsync();

            vm.SelectedQuestions.Add(frage);

            return vm;
        }

        /// <summary>Eine Frage, die in keinem Spielfeld liegt.</summary>
        private static async Task<QuestionBase> FreieFrageAsync(Guid kategorieId)
        {
            var frage = new Quizzer.DataModels.Models.QuestionTypes.DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Freie Frage",
                DesignationShort = "FF",
                CategoryId = kategorieId,
                Points = 100,
            };

            using var ctrl = new QuestionBasesController();

            await ctrl.UpsertAsync(frage);
            await ctrl.SaveChangesAsync();

            return frage;
        }

        /// <summary>
        /// Die Frage des Testspiels liegt auf einer Zelle - das Löschen wird abgelehnt, und die
        /// Meldung nennt das Spiel.
        /// </summary>
        [TestMethod]
        public async Task AQuestionOnAGridIsRefusedNamingTheGame()
        {
            var vm = await BuildViewModelAsync(world.Question);

            await TestEnvironment.RunCommandAsync(vm.RemoveQuestionCommand);

            Assert.AreEqual(1, prompt.Informs.Count,
                "Es kam keine Meldung - der Spielleiter saehe nur einen Datenbankfehler.");

            StringAssert.Contains(prompt.Informs[0].Message, world.Game.Designation,
                "Die Meldung nennt das Spiel nicht, in dem die Frage liegt. Sie lautete: "
                + prompt.Informs[0].Message);

            Assert.AreEqual(0, prompt.Confirms.Count,
                "Es wurde zusaetzlich gefragt, obwohl schon abgelehnt wurde.");

            using var ctrl = new QuestionBasesController();

            Assert.IsNotNull(await ctrl.GetAsync(world.Question.Id),
                "Die Frage wurde trotz der Ablehnung geloescht.");
        }

        /// <summary>
        /// Eine freie Frage darf gelöscht werden - aber erst nach Rückfrage.
        /// Die Gegenrichtung: ohne sie wäre die Probe oben auch dann grün, wenn grundsätzlich
        /// jede Löschung abgelehnt würde.
        /// </summary>
        [TestMethod]
        public async Task AFreeQuestionIsOnlyDeletedAfterAsking()
        {
            var frei = await FreieFrageAsync(world.Question.CategoryId);

            try
            {
                var vm = await BuildViewModelAsync(frei);

                await TestEnvironment.RunCommandAsync(vm.RemoveQuestionCommand);

                Assert.AreEqual(0, prompt.Informs.Count,
                    "Eine freie Frage darf nicht abgelehnt werden: "
                    + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

                Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde nicht gefragt.");

                StringAssert.Contains(prompt.Confirms[0].Message, "Freie Frage",
                    "Die Rueckfrage nennt die Frage nicht.");

                using var ctrl = new QuestionBasesController();

                Assert.IsNotNull(await ctrl.GetAsync(frei.Id),
                    "Die Frage wurde geloescht, obwohl die Rueckfrage verneint wurde.");
            }
            finally
            {
                using var ctrl = new QuestionBasesController();

                await ctrl.DeleteAsync(frei.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        /// <summary>Ohne Auswahl geschieht nichts - weder Frage noch Meldung.</summary>
        [TestMethod]
        public async Task WithoutASelectionNothingHappens()
        {
            var vm = new QuestionsViewModel();

            await vm.LoadForTestAsync();

            await TestEnvironment.RunCommandAsync(vm.RemoveQuestionCommand);

            Assert.AreEqual(0, prompt.Confirms.Count + prompt.Informs.Count,
                "Es wurde etwas gemeldet, obwohl nichts ausgewaehlt ist.");
        }
    }
}
