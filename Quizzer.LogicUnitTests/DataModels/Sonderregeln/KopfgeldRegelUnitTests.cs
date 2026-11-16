using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Kopfgeld (M35 C3b) als reine Rechnung: Gesuchte = hoechster Kontostand (alle gleichauf: keiner), je Paar wandert der Anteil, das Minus des
    /// Gesuchten wird einmal gekappt, ein Plus nie.
    /// </summary>
    [TestClass]
    public class KopfgeldRegelUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Clara = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();

        private static string Name(Guid id) => id == Anna ? "Anna" : id == Bert ? "Bert" : id == Clara ? "Clara" : "Dora";

        private static SonderregelEingang E(RatingKind bewertung, int grundwert = 0, int kontostand = 500, bool unterNull = true)
            => new(bewertung, grundwert, 100, new Kappung(kontostand, unterNull));

        // --- Kopfgeld ---

        [TestMethod]
        public void TheWantedAreTheLeadersAndNobodyWhenAllAreTied()
        {
            CollectionAssert.AreEquivalent(new[] { Anna, Bert },
                KopfgeldRegel.Gesuchte(new Dictionary<Guid, int> { [Anna] = 90, [Bert] = 90, [Clara] = 10 }).ToList(), "Nicht alle Fuehrenden gesucht.");
            Assert.AreEqual(0, KopfgeldRegel.Gesuchte(new Dictionary<Guid, int> { [Anna] = 50, [Bert] = 50 }).Count, "Alle gleichauf - trotzdem gesucht.");
            Assert.AreEqual(0, KopfgeldRegel.Gesuchte(new Dictionary<Guid, int>()).Count);
        }

        [TestMethod]
        public void EachPairMovesTheBountyFromTheWantedToTheHunter()
        {
            // Anna gesucht, nicht richtig; Bert und Clara jagen richtig, Dora falsch.
            var eingaenge = new Dictionary<Guid, SonderregelEingang>
            {
                [Anna] = E(RatingKind.Wrong, -30), [Bert] = E(RatingKind.Right, 100), [Clara] = E(RatingKind.Right, 100), [Dora] = E(RatingKind.Wrong, -30),
            };

            var v = KopfgeldRegel.Werte([Anna], eingaenge, 20, Name);

            Assert.AreEqual(120, v[Bert].Punkte, "Der Jaeger bekommt das Kopfgeld nicht zu seinen Punkten.");
            Assert.AreEqual(120, v[Clara].Punkte);
            Assert.AreEqual(-30 - 40, v[Anna].Punkte, "Der Gesuchte zahlt nicht je Jaeger.");
            Assert.IsFalse(v.ContainsKey(Dora), "Ein falscher Jaeger bekommt etwas.");
            StringAssert.Contains(v[Bert].Grund, "Kopfgeld von Anna");

            // Der Gesuchte ist richtig: kein Paar, nichts wandert.
            eingaenge[Anna] = E(RatingKind.Right, 100);
            Assert.AreEqual(0, KopfgeldRegel.Werte([Anna], eingaenge, 20, Name).Count, "Ein richtiger Gesuchter zahlt.");
        }

        [TestMethod]
        public void TheWantedsMinusIsCappedOnceTheHuntersPlusNever()
        {
            var eingaenge = new Dictionary<Guid, SonderregelEingang>
            {
                [Anna] = E(RatingKind.None, 0, kontostand: 25, unterNull: false), [Bert] = E(RatingKind.Right, 100), [Clara] = E(RatingKind.Right, 100),
            };

            var v = KopfgeldRegel.Werte([Anna], eingaenge, 20, Name);

            Assert.AreEqual(-25, v[Anna].Punkte, "Das Minus des Gesuchten ist nicht einmal am Kontostand gekappt.");
            StringAssert.Contains(v[Anna].Grund, "gekappt");
            Assert.AreEqual(120, v[Bert].Punkte, "Das Plus des Jaegers ist mitgekappt.");

            eingaenge[Anna] = E(RatingKind.None, 0, kontostand: 25, unterNull: true);
            Assert.AreEqual(-40, KopfgeldRegel.Werte([Anna], eingaenge, 20, Name)[Anna].Punkte, "Gekappt, obwohl das Spiel ein Minus erlaubt.");
        }
    }
}
