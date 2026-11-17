using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// Der Feldwert, auf die Mitglieder verteilt (M37 T5, F-M37-06): zur Null hin, der Rest als ±1 vorn in der
    /// Sprecherfolge, die Summe immer der Wert - auch negativ (Minuspunkte, aufgeteilt).
    /// </summary>
    [TestClass]
    public class AufteilungUnitTests
    {
        private static Guid[] Ids(int anzahl) => Enumerable.Range(0, anzahl).Select(_ => Guid.NewGuid()).ToArray();

        [TestMethod]
        [DataRow(100, new[] { 34, 33, 33 })]
        [DataRow(-100, new[] { -34, -33, -33 })]
        [DataRow(101, new[] { 34, 34, 33 })]
        [DataRow(-101, new[] { -34, -34, -33 })]
        [DataRow(300, new[] { 100, 100, 100 })]
        [DataRow(2, new[] { 1, 1, 0 })]
        [DataRow(-2, new[] { -1, -1, 0 })]
        [DataRow(0, new[] { 0, 0, 0 })]
        public void TheRestGoesToTheFirstOfTheSpeakerOrder(int wert, int[] erwartet)
        {
            var folge = Ids(3);

            var anteile = Aufteilung.Verteile(wert, folge);

            CollectionAssert.AreEqual(erwartet, folge.Select(id => anteile[id]).ToArray(), $"{wert} auf drei");
        }

        /// <summary>Über viele Werte und Größen: die Summe ist der Wert, und die Anteile liegen höchstens eins auseinander.</summary>
        [TestMethod]
        public void TheSumIsAlwaysTheValueAndTheSharesDifferByAtMostOne()
        {
            var geprueft = 0;

            for (var mitglieder = 1; mitglieder <= TeamGrenzen.MaxTeams + 4; mitglieder++)
            for (var wert = -1000; wert <= 1000; wert += 7)
            {
                var anteile = Aufteilung.Verteile(wert, Ids(mitglieder)).Values.ToList();
                var fall = $"{wert} auf {mitglieder}: {string.Join("/", anteile)}";

                Assert.AreEqual(mitglieder, anteile.Count, fall);
                Assert.AreEqual(wert, anteile.Sum(), "Die Summe ist nicht der Wert. " + fall);
                Assert.IsTrue(anteile.Max() - anteile.Min() <= 1, "Die Anteile liegen weiter als eins auseinander. " + fall);
                Assert.IsTrue(anteile.All(a => a == 0 || Math.Sign(a) == Math.Sign(wert)), "Ein Anteil hat das falsche Vorzeichen. " + fall);
                geprueft++;
            }

            Assert.IsTrue(geprueft > 3000, $"Nur {geprueft} Fälle - die Schleife greift ins Leere.");
        }

        [TestMethod]
        public void TheExtremesKeepTheSum()
        {
            foreach (var wert in new[] { int.MaxValue, int.MinValue, int.MinValue + 1 })
                Assert.AreEqual((long)wert, Aufteilung.Verteile(wert, Ids(3)).Values.Sum(a => (long)a), $"{wert} auf drei");
        }

        [TestMethod]
        public void WithoutMembersThereIsNothingToShare()
        {
            Assert.AreEqual(0, Aufteilung.Verteile(300, []).Count);
        }

        /// <summary>Eine doppelte Kennung bekäme zwei Anteile in einen - und die Summe wäre nicht mehr der Wert.</summary>
        [TestMethod]
        public void ADuplicateCountsOnce()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var anteile = Aufteilung.Verteile(100, [a, b, a]);

            Assert.AreEqual(2, anteile.Count);
            Assert.AreEqual(100, anteile.Values.Sum());
            Assert.AreEqual(50, anteile[a]);
        }
    }
}
