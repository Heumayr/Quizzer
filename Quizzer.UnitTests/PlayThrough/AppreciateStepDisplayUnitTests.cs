using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using System.Windows;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Was die Schaetzfrage auf dem Bildschirm zeigt. Der heikle Punkt: der richtige Wert darf
    /// nicht vorzeitig auf dem Beamer stehen.
    /// </summary>
    [TestClass]
    public class AppreciateStepDisplayUnitTests
    {
        private static QuestionStepViewContext ContextFor(
            bool isMasterView, bool isFinishStep, AppreciateQestion? question = null)
        {
            question ??= new AppreciateQestion
            {
                ValueKind = AppreciateValueKind.Length,
                Unit = AppreciateUnit.Meter,
                ExpectedValue = 3798,
            };

            var owner = new CurrentQuestionViewModel
            {
                Coordinate = new GameGridCoordinate { QuestionBase = question },
            };

            return new QuestionStepViewContext
            {
                Owner = owner,
                IsMasterView = isMasterView,
                Step = new QuestionStepResource { Id = Guid.NewGuid(), IsFinish = isFinishStep },
            };
        }

        /// <summary>
        /// Sonst stuende die Antwort auf dem Beamer, bevor jemand geschaetzt hat.
        /// </summary>
        [TestMethod]
        public void ThePlayersDoNotSeeTheAnswerBeforeTheFinishStep()
        {
            var context = ContextFor(isMasterView: false, isFinishStep: false);

            Assert.AreEqual(Visibility.Collapsed, context.AppreciateExpectedVisibility);
        }

        [TestMethod]
        public void ThePlayersSeeTheAnswerOnTheFinishStep()
        {
            var context = ContextFor(isMasterView: false, isFinishStep: true);

            Assert.AreEqual(Visibility.Visible, context.AppreciateExpectedVisibility);
            StringAssert.Contains(context.AppreciateExpectedText, "3798");
        }

        [TestMethod]
        public void TheGameMasterSeesTheAnswerFromTheStart()
        {
            var context = ContextFor(isMasterView: true, isFinishStep: false);

            Assert.AreEqual(Visibility.Visible, context.AppreciateExpectedVisibility);
        }

        [TestMethod]
        public void WhileGuessingThePromptNamesTheUnit()
        {
            var context = ContextFor(isMasterView: false, isFinishStep: false);

            Assert.AreEqual(Visibility.Visible, context.AppreciateAskVisibility);
            StringAssert.Contains(context.AppreciateAskText, "m");
        }

        [TestMethod]
        public void OnTheFinishStepThePromptIsGone()
        {
            var context = ContextFor(isMasterView: false, isFinishStep: true);

            Assert.AreEqual(Visibility.Collapsed, context.AppreciateAskVisibility);
        }

        [TestMethod]
        public void ADateQuestionShowsTheDate()
        {
            var question = new AppreciateQestion
            {
                ValueKind = AppreciateValueKind.Date,
                Unit = AppreciateUnit.Datum,
                ExpectedDate = new DateTime(1969, 7, 20),
            };

            var context = ContextFor(isMasterView: true, isFinishStep: true, question);

            StringAssert.Contains(context.AppreciateExpectedText, "1969");
        }

        [TestMethod]
        public void OtherQuestionTypesShowNothingOfThis()
        {
            var owner = new CurrentQuestionViewModel
            {
                Coordinate = new GameGridCoordinate { QuestionBase = new DefaultQuestion() },
            };

            var context = new QuestionStepViewContext
            {
                Owner = owner,
                IsMasterView = true,
                Step = new QuestionStepResource { Id = Guid.NewGuid() },
            };

            Assert.IsNull(context.AppreciateQuestion);
            Assert.AreEqual(Visibility.Collapsed, context.AppreciateExpectedVisibility);
            Assert.AreEqual(Visibility.Collapsed, context.AppreciateAskVisibility);
        }
    }
}
