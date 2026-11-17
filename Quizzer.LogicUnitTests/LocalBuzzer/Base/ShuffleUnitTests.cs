using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Das Mischen von Blitzrunde und Heisser Kartoffel an einer Stelle (V13): jede Zahl genau einmal, gewuerfelt von
    /// hinten, und ein Wurf ausserhalb des Bereichs bleibt im Bereich.
    /// </summary>
    [TestClass]
    public class ShuffleUnitTests
    {
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(7)]
        public void EveryNumberComesExactlyOnce(int anzahl)
        {
            for (var saat = 0; saat < 20; saat++)
            {
                var folge = Shuffle.Order(anzahl, new Random(saat).Next);

                CollectionAssert.AreEquivalent(Enumerable.Range(0, anzahl).ToList(), folge, $"Keine Mischung von 0 bis {anzahl - 1} (Saat {saat}).");
            }
        }

        /// <summary>Die Reihenfolge der Wuerfe ist fest - Tests der Arten setzen die Quelle und bauen darauf.</summary>
        [TestMethod]
        public void TheDrawsRunFromTheBackAndAlwaysSwap()
        {
            var wuerfe = new List<(int Von, int Bis)>();

            var folge = Shuffle.Order(4, (von, bis) => { wuerfe.Add((von, bis)); return 0; });

            CollectionAssert.AreEqual(new[] { (0, 4), (0, 3), (0, 2) }, wuerfe, "Die Wuerfe laufen nicht von hinten.");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, folge, "Immer die erste Stelle getauscht ergibt eine andere Folge.");
        }

        [TestMethod]
        public void ADrawOutsideTheRangeStaysInside()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Shuffle.Order(4, (_, _) => 99), "Ein zu grosser Wurf tauscht ausserhalb.");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, Shuffle.Order(4, (_, _) => -5), "Ein negativer Wurf tauscht ausserhalb.");
        }
    }
}
