using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;

namespace Quizzer.LogicUnitTests.DataModels.Helpers
{
    /// <summary>
    /// Die Anzeige-Schluessel der Schritte (A, B, C ... oder 1, 2, 3 ...). Das ist, was der
    /// Spieler im Browser als Antworttaste sieht.
    /// </summary>
    [TestClass]
    public class HelperUnitTests
    {
        [TestMethod]
        [DataRow("", "A")]
        [DataRow("A", "B")]
        [DataRow("Y", "Z")]
        [DataRow("Z", "AA")]
        [DataRow("AA", "AB")]
        [DataRow("AZ", "BA")]
        [DataRow("ZZ", "AAA")]
        public void GetNextViewKey_Alphabetical_CountsLikeSpreadsheetColumns(string current, string expected)
        {
            Assert.AreEqual(expected, Helper.GetNextViewKey(current, QuestionViewKeyType.Alphabetical));
        }

        [TestMethod]
        [DataRow("a", "B")]
        [DataRow("  a  ", "B")]
        public void GetNextViewKey_Alphabetical_IgnoresCasingAndSpaces(string current, string expected)
        {
            Assert.AreEqual(expected, Helper.GetNextViewKey(current, QuestionViewKeyType.Alphabetical));
        }

        [TestMethod]
        [DataRow("", "1")]
        [DataRow("1", "2")]
        [DataRow("9", "10")]
        [DataRow("99", "100")]
        public void GetNextViewKey_Numerical_CountsUp(string current, string expected)
        {
            Assert.AreEqual(expected, Helper.GetNextViewKey(current, QuestionViewKeyType.Numerical));
        }

        [TestMethod]
        public void GetNextViewKey_Alphabetical_RejectsNonLetters()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                Helper.GetNextViewKey("A1", QuestionViewKeyType.Alphabetical));
        }

        [TestMethod]
        public void GetNextViewKey_Numerical_RejectsNonNumbers()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                Helper.GetNextViewKey("A", QuestionViewKeyType.Numerical));
        }

        [TestMethod]
        public void GetNextViewKey_RejectsUnknownKeyType()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                Helper.GetNextViewKey("A", (QuestionViewKeyType)99));
        }
    }
}
