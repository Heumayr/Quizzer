using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.LogicUnitTests.DataModels.Models
{
    /// <summary>
    /// Reihenfolge und Navigation der Frageschritte - der Kern des Durchspielens.
    /// </summary>
    [TestClass]
    public class QuestionBaseUnitTests
    {
        [TestCleanup]
        public void RestoreRandomizer() => QuestionBase.Randomizer = Random.Shared;

        private static QuestionStepResource Step(string designation, int sequenceNumber,
            bool isResult = false, bool isFinish = false)
            => new()
            {
                Id = Guid.NewGuid(),
                Designation = designation,
                SequenceNumber = sequenceNumber,
                IsResult = isResult,
                IsFinish = isFinish,
            };

        private static DefaultQuestion QuestionWith(params QuestionStepResource[] steps)
        {
            var question = new DefaultQuestion();
            question.Steps.AddRange(steps);
            return question;
        }

        [TestMethod]
        public void CalculateOrderdSteps_WithoutSteps_YieldsNothing()
        {
            var question = QuestionWith();

            question.CalculateOrderdSteps();

            Assert.AreEqual(0, question.OrderedSteps.Length);
        }

        [TestMethod]
        public void CalculateOrderdSteps_SortsNormalStepsBySequenceNumber()
        {
            var question = QuestionWith(
                Step("dritter", 30), Step("erster", 10), Step("zweiter", 20));

            question.CalculateOrderdSteps();

            var normal = question.OrderedSteps.Where(s => !s.IsStart && !s.IsFinish).ToList();
            CollectionAssert.AreEqual(
                new[] { "erster", "zweiter", "dritter" },
                normal.Select(s => s.Designation).ToArray());
        }

        [TestMethod]
        public void CalculateOrderdSteps_RenumbersInStepsOfTen()
        {
            var question = QuestionWith(Step("a", 7), Step("b", 999));

            question.CalculateOrderdSteps();

            CollectionAssert.AreEqual(
                new[] { 0, 10, 20, 30 },
                question.OrderedSteps.Select(s => s.SequenceNumber).ToArray());
        }

        [TestMethod]
        public void CalculateOrderdSteps_AppendsFinishStepWhenNoneAuthored()
        {
            var question = QuestionWith(Step("a", 10));

            question.CalculateOrderdSteps();

            Assert.IsTrue(question.OrderedSteps.Last().IsFinish);
        }

        [TestMethod]
        public void CalculateOrderdSteps_KeepsAuthoredFinishStepLast()
        {
            var question = QuestionWith(
                Step("aufloesung", 99, isResult: true, isFinish: true), Step("hinweis", 10));

            question.CalculateOrderdSteps();

            Assert.AreEqual("aufloesung", question.OrderedSteps.Last().Designation);
        }

        /// <summary>
        /// Haelt den HEUTIGEN Stand fest: ein Startschritt laesst sich nicht anlegen, weil IsStart
        /// nicht gespeichert wird - deshalb erfindet CalculateOrderdSteps immer einen leeren.
        /// Der erste Druck auf Weiter zeigt dadurch einen leeren Bildschirm.
        /// Diese Zusicherung wird umgedreht, sobald IsStart eine echte Spalte ist.
        /// </summary>
        [TestMethod]
        public void CalculateOrderdSteps_AlwaysInventsAnEmptyStartStep_KnownShortcoming()
        {
            var question = QuestionWith(Step("hinweis", 10));

            question.CalculateOrderdSteps();

            var first = question.OrderedSteps.First();
            Assert.IsTrue(first.IsStart);
            Assert.AreEqual(string.Empty, first.StepText);
            Assert.AreEqual(string.Empty, first.Designation);
            Assert.IsFalse(question.Steps.Contains(first));
        }

        [TestMethod]
        public void CalculateOrderdSteps_AssignsViewKeysOnlyToNormalSteps()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20), Step("c", 30));

            question.CalculateOrderdSteps();

            var keys = question.OrderedSteps.Select(s => s.QuestionViewKey).ToArray();
            CollectionAssert.AreEqual(new[] { string.Empty, "A", "B", "C", string.Empty }, keys);
        }

        [TestMethod]
        public void CalculateOrderdSteps_UsesNumericKeysWhenConfigured()
        {
            var question = new PropertiesQuestion();
            question.Steps.AddRange([Step("a", 10), Step("b", 20)]);

            question.CalculateOrderdSteps();

            var keys = question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.QuestionViewKey).ToArray();
            CollectionAssert.AreEqual(new[] { "1", "2" }, keys);
        }

        [TestMethod]
        public void CalculateOrderdSteps_WhenShuffling_KeepsEveryNormalStepExactlyOnce()
        {
            var question = new MultipleChoiceQuestion();
            question.Steps.AddRange([Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)]);
            Assert.IsTrue(question.UseRandomSequenceOnNoneFinishSteps);

            question.CalculateOrderdSteps();

            var names = question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation)
                .OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, names);
        }

        [TestMethod]
        public void CalculateOrderdSteps_WhenShuffling_SameSeedGivesSameOrder()
        {
            QuestionBase.Randomizer = new Random(4711);
            var question = new MultipleChoiceQuestion();
            question.Steps.AddRange([Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)]);
            question.CalculateOrderdSteps();
            var first = question.OrderedSteps.Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation).ToArray();

            QuestionBase.Randomizer = new Random(4711);
            var again = new MultipleChoiceQuestion();
            again.Steps.AddRange([Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)]);
            again.CalculateOrderdSteps();
            var second = again.OrderedSteps.Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation).ToArray();

            CollectionAssert.AreEqual(first, second);
        }

        [TestMethod]
        public void GetNextStep_WalksForwardAndStopsAtTheEnd()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20));
            question.CalculateOrderdSteps();

            var current = question.OrderedSteps.First();
            var visited = 1;

            while (question.GetNextStep(current) is { } next)
            {
                current = next;
                visited++;
            }

            Assert.AreEqual(question.OrderedSteps.Length, visited);
            Assert.IsTrue(current.IsFinish);
        }

        [TestMethod]
        public void GetStepBehind_WalksBackOneStep()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20));
            question.CalculateOrderdSteps();

            var back = question.GetStepBehind(question.OrderedSteps.Last());

            Assert.AreEqual("b", back?.Designation);
        }

        [TestMethod]
        public void GetStepBehind_FromFirstStep_YieldsNothing()
        {
            var question = QuestionWith(Step("a", 10));
            question.CalculateOrderdSteps();

            Assert.IsNull(question.GetStepBehind(question.OrderedSteps.First()));
        }
    }
}
