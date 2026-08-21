using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.DataModels.Models.Base
{
    /// <summary>Der Punktestand eines Spielers zu einer Frage.</summary>
    [TestClass]
    public class QuestionResultUnitTests
    {
        [TestMethod]
        [DataRow(100, 0, 0, 100)]
        [DataRow(100, 0, 40, 60)]
        [DataRow(100, 25, 0, 125)]
        [DataRow(0, -50, 0, -50)]
        [DataRow(100, -10, 40, 50)]
        public void FinalScore_IsScorePlusCorrectionMinusMinusScore(
            int score, int correction, int minusScore, int expected)
        {
            var result = new QuestionResult
            {
                Score = score,
                Correction = correction,
                MinusScore = minusScore,
            };

            Assert.AreEqual(expected, result.FinalScore);
        }

        [TestMethod]
        public void FinalScore_CanGoNegative()
        {
            var result = new QuestionResult { Score = 0, MinusScore = 200 };

            Assert.AreEqual(-200, result.FinalScore,
                "Wer nur falsch liegt, steht im Minus - das ist gewollt.");
        }
    }
}
