using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Die proportionale Punktereduktion der Eigenschaftsfrage haengt an
    /// <c>RegularStepCount</c>, und das zaehlt Schritte mit <c>!IsStart &amp;&amp; !IsFinish</c>.
    /// <para>
    /// Seit der Startschritt eine echte Spalte ist und keiner mehr erfunden wird, aendert sich
    /// die Menge dieser Schritte - deshalb wird hier belegt statt angenommen, dass die
    /// Rechnung dieselbe bleibt.
    /// </para>
    /// </summary>
    [TestClass]
    public class PropertiesQuestionScoringUnitTests
    {
        private static QuestionStepResource Step(string name, int sequence, bool isStart = false,
            bool isFinish = false)
            => new()
            {
                Id = Guid.NewGuid(),
                Designation = name,
                StepText = name,
                SequenceNumber = sequence,
                IsStart = isStart,
                IsFinish = isFinish,
            };

        private static PropertiesQuestion QuestionWith(params QuestionStepResource[] steps)
        {
            var question = new PropertiesQuestion { Points = 120 };
            question.Steps.AddRange(steps);
            question.CalculateOrderdSteps();
            return question;
        }

        private static int RegularStepCount(QuestionBase question)
            => question.OrderedSteps.Count(s => !s.IsStart && !s.IsFinish);

        [TestMethod]
        public void ThreeHints_CountAsThreeRegularSteps()
        {
            var question = QuestionWith(Step("H1", 10), Step("H2", 20), Step("H3", 30));

            Assert.AreEqual(3, RegularStepCount(question));
        }

        [TestMethod]
        public void AnAuthoredStartStep_DoesNotCountAsARegularStep()
        {
            var question = QuestionWith(
                Step("Intro", 5, isStart: true), Step("H1", 10), Step("H2", 20), Step("H3", 30));

            Assert.AreEqual(3, RegularStepCount(question),
                "Ein Intro darf die erreichbaren Punkte nicht verwaessern.");
        }

        [TestMethod]
        public void TheFinishStep_DoesNotCountEither()
        {
            var question = QuestionWith(
                Step("H1", 10), Step("H2", 20), Step("Aufloesung", 99, isFinish: true));

            Assert.AreEqual(2, RegularStepCount(question));
        }

        /// <summary>
        /// Rechnet die Formel aus PlayerResultContext.GetCurrentPoints nach, damit die
        /// Ganzzahldivision belegt ist: 120 / 3 * 2 = 80 Abzug, es bleiben 40.
        /// </summary>
        [TestMethod]
        [DataRow(0, 120)]
        [DataRow(1, 80)]
        [DataRow(2, 40)]
        [DataRow(3, 0)]
        public void ScoreShrinksWithEachRevealedHint(int previousSteps, int expected)
        {
            var question = QuestionWith(Step("H1", 10), Step("H2", 20), Step("H3", 30));
            var points = question.Points;

            var reduction = points / RegularStepCount(question) * previousSteps;

            Assert.AreEqual(expected, points - reduction);
        }

        [TestMethod]
        public void WithAnIntro_TheScoreStepsStayTheSame()
        {
            var withoutIntro = QuestionWith(Step("H1", 10), Step("H2", 20), Step("H3", 30));
            var withIntro = QuestionWith(
                Step("Intro", 5, isStart: true), Step("H1", 10), Step("H2", 20), Step("H3", 30));

            var a = withoutIntro.Points / RegularStepCount(withoutIntro);
            var b = withIntro.Points / RegularStepCount(withIntro);

            Assert.AreEqual(a, b, "Der Abzug je Hinweis darf sich durch ein Intro nicht aendern.");
        }

        [TestMethod]
        public void TheTypeStillAsksForProportionalScoring()
        {
            var question = QuestionWith(Step("H1", 10));

            Assert.IsTrue(question.UseProportionalScoreReductionOnStep);
            Assert.AreEqual(QuestionType.Properties, question.Typ);
        }
    }
}
