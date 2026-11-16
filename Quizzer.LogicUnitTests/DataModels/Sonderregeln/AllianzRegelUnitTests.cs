using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Allianz (M35 C3b) als reine Rechnung: nur beide richtig bringt Plus, ein Minus bleibt.
    /// </summary>
    [TestClass]
    public class AllianzRegelUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Clara = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();

        private static string Name(Guid id) => id == Anna ? "Anna" : id == Bert ? "Bert" : id == Clara ? "Clara" : "Dora";

        private static SonderregelEingang E(RatingKind bewertung, int grundwert = 0, int kontostand = 500, bool unterNull = true)
            => new(bewertung, grundwert, 100, new Kappung(kontostand, unterNull));

        // --- Allianz ---

        [TestMethod]
        public void TheAllianceGainsOnlyWhenBothAreRightAndKeepsAMinus()
        {
            var eingaenge = new Dictionary<Guid, SonderregelEingang> { [Anna] = E(RatingKind.Right, 100), [Bert] = E(RatingKind.Right, 100) };

            var beide = AllianzRegel.Werte(Anna, Bert, eingaenge);
            Assert.AreEqual(100, beide[Anna].Punkte, "Beide richtig bringt nicht die vollen Punkte.");
            Assert.AreEqual(100, beide[Bert].Punkte);

            eingaenge[Bert] = E(RatingKind.Wrong, -50);
            var einer = AllianzRegel.Werte(Anna, Bert, eingaenge);
            Assert.AreEqual(0, einer[Anna].Punkte, "Nur einer richtig - trotzdem ein Plus.");
            Assert.IsFalse(einer.ContainsKey(Bert), "Das Minus des Partners ist nicht seiner Bewertung ueberlassen.");

            eingaenge[Bert] = E(RatingKind.Partial, 40);
            Assert.AreEqual(0, AllianzRegel.Werte(Anna, Bert, eingaenge)[Bert].Punkte, "Ein Teilwert gilt als richtig.");
        }

    }
}
