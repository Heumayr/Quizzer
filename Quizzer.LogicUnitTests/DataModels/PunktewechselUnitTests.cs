using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Anzeige;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>Die Einblendung der Punkteänderungen nach einer Frage (2026-10-21).</summary>
    [TestClass]
    public class PunktewechselUnitTests
    {
        [TestMethod]
        public void TheChangeReadsWithItsSign()
        {
            Assert.AreEqual("+200", Punktewechsel.Text(200));
            Assert.AreEqual("−100", Punktewechsel.Text(-100), "Kein echtes Minuszeichen.");
            Assert.AreEqual(string.Empty, Punktewechsel.Text(0), "Ohne Aenderung steht etwas da.");
        }

        [TestMethod]
        public void CountingStartsAtTheOldAndEndsExactlyAtTheNewScore()
        {
            Assert.AreEqual(300, Punktewechsel.Zwischenstand(300, 500, 0));
            Assert.AreEqual(500, Punktewechsel.Zwischenstand(300, 500, 1));
            Assert.AreEqual(500, Punktewechsel.Zwischenstand(300, 500, 7), "Ueber das Ende hinaus wird weitergezaehlt.");
            Assert.AreEqual(300, Punktewechsel.Zwischenstand(300, 500, -1));
        }

        /// <summary>Monoton in beide Richtungen - die Zahl springt nie zurück.</summary>
        [TestMethod]
        [DataRow(300, 500)]
        [DataRow(500, 300)]
        [DataRow(0, -150)]
        public void CountingNeverJumpsBack(int alt, int neu)
        {
            var schritte = Enumerable.Range(0, 61).Select(i => Punktewechsel.Zwischenstand(alt, neu, i / 60.0)).ToList();

            for (var i = 1; i < schritte.Count; i++)
                Assert.IsTrue(neu >= alt ? schritte[i] >= schritte[i - 1] : schritte[i] <= schritte[i - 1],
                    $"Bei Schritt {i} springt die Zahl zurueck: {schritte[i - 1]} -> {schritte[i]}");

            // Auslaufend: nach der halben Zeit ist mehr als die Haelfte geschafft.
            Assert.IsTrue(Math.Abs(schritte[30] - alt) > Math.Abs(neu - alt) / 2);
        }
    }
}
