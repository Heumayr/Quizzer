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
                new[] { 0, 10, 20 },
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
        /// Bis zum 21.08.2026 erfand CalculateOrderdSteps hier immer einen leeren Startschritt,
        /// weil IsStart nicht gespeichert wurde - der erste Druck auf Weiter zeigte deshalb
        /// einen leeren Bildschirm. Jetzt beginnt die Frage mit dem ersten echten Schritt.
        /// </summary>
        [TestMethod]
        public void CalculateOrderdSteps_WithoutAnAuthoredStartStep_BeginsWithTheFirstRealStep()
        {
            var question = QuestionWith(Step("hinweis", 10));

            question.CalculateOrderdSteps();

            var first = question.OrderedSteps.First();
            Assert.IsFalse(first.IsStart);
            Assert.AreEqual("hinweis", first.Designation);
            Assert.IsTrue(question.Steps.Contains(first),
                "Kein erfundener Schritt mehr - alles Angezeigte gehoert der Frage.");
        }

        [TestMethod]
        public void CalculateOrderdSteps_PutsAnAuthoredStartStepFirst()
        {
            var question = QuestionWith(Step("hinweis", 10));
            var intro = Step("Intro", 5);
            intro.IsStart = true;
            question.Steps.Add(intro);

            question.CalculateOrderdSteps();

            Assert.AreEqual("Intro", question.OrderedSteps.First().Designation);
            Assert.IsTrue(question.OrderedSteps.First().IsStart);
        }

        [TestMethod]
        public void CalculateOrderdSteps_GivesTheStartStepNoAnswerKey()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20));
            var intro = Step("Intro", 5);
            intro.IsStart = true;
            question.Steps.Add(intro);

            question.CalculateOrderdSteps();

            Assert.AreEqual(string.Empty, question.OrderedSteps.First().QuestionViewKey);
            var keys = question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.QuestionViewKey).ToArray();
            CollectionAssert.AreEqual(new[] { "A", "B" }, keys);
        }

        [TestMethod]
        public void CalculateOrderdSteps_AssignsViewKeysOnlyToNormalSteps()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20), Step("c", 30));

            question.CalculateOrderdSteps();

            var keys = question.OrderedSteps.Select(s => s.QuestionViewKey).ToArray();
            CollectionAssert.AreEqual(new[] { "A", "B", "C", string.Empty }, keys,
                "Nur der Abschlussschritt bleibt ohne Antworttaste.");
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
