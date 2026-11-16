using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Das Memory (M35 C5) als reine Funktion: jede Belegung traegt jedes Motiv genau zweimal und haengt nur
    /// an den Wuerfen, und die Wertung - zuerst fertig, sonst die meisten Paare, mit Gleichstand und Kappung (E2).
    /// </summary>
    [TestClass]
    public class MemoryUnitTests
    {
        private const int Feld = 200;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static MemoryErgebnis Werte(Dictionary<Guid, MemoryStand> staende, Kontostaende? konten = null)
            => MemoryEvaluator.Werte(staende, 6, Feld, konten ?? Kontostaende.OhneGrenze);

        private static MemoryStand Fertig(double sekunden) => new(6, TimeSpan.FromSeconds(sekunden));

        [TestMethod]
        [DataRow(0, 6)]
        [DataRow(1, 8)]
        [DataRow(2, 10)]
        public void EveryLayoutHoldsEachMotifExactlyTwice(int variante, int paare)
        {
            Assert.AreEqual(paare, MemoryBelegung.PaareFuer(variante));

            for (var seed = 0; seed < 200; seed++)
            {
                var zufall = new Random(seed);
                var belegung = MemoryBelegung.Mische(paare, zufall.Next);

                Assert.AreEqual(paare * 2, belegung.Length);
                Assert.IsTrue(belegung.GroupBy(m => m).All(g => g.Count() == 2), $"Wurf {seed}: ein Motiv liegt nicht genau zweimal.");
                Assert.AreEqual(paare, belegung.Distinct().Count(), $"Wurf {seed}: zwei Paare teilen ein Motiv.");
                Assert.IsTrue(belegung.All(m => m >= 0 && m < MemoryBelegung.Motive.Count));
            }
        }

        [TestMethod]
        public void TheLayoutDependsOnlyOnTheThrowsAndChangesWithThem()
        {
            var eins = MemoryBelegung.Mische(8, new Random(7).Next);
            var nochmal = MemoryBelegung.Mische(8, new Random(7).Next);
            var anders = MemoryBelegung.Mische(8, new Random(8).Next);

            CollectionAssert.AreEqual(eins, nochmal, "Dieselben Wuerfe geben eine andere Belegung.");
            CollectionAssert.AreNotEqual(eins, anders, "Andere Wuerfe geben dieselbe Belegung.");
        }

        /// <summary>Die Reihenfolge der Wuerfe ist zugesagt - die Zustandstests des Servers bauen ihre Belegung darauf.</summary>
        [TestMethod]
        public void TheThrowOrderIsFixed()
        {
            var belegung = MemoryBelegung.Mische(6, (von, bis) => bis == MemoryBelegung.Motive.Count ? von : bis - 1);

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, belegung);
            Assert.AreEqual(MemoryBelegung.Motive.Count, MemoryBelegung.Motive.Select(m => m.Symbol).Distinct().Count(), "Zwei Motive sehen gleich aus.");
            Assert.IsTrue(MemoryBelegung.Motive.Count >= MemoryBelegung.PaareFuer(2), "Fuer die groesste Spielweise fehlen Motive.");
        }

        [TestMethod]
        public void TheFirstToFinishWinsAndTheRestRankByPairs()
        {
            var ergebnis = Werte(new()
            {
                [Anna] = Fertig(52.3),
                [Bert] = Fertig(41.0),
                [Carl] = new(5, null),
            });

            CollectionAssert.AreEqual(new[] { Bert, Anna, Carl }, ergebnis.Rangfolge.ToList());
            CollectionAssert.AreEquivalent(new[] { Bert }, ergebnis.Gewinner.ToList());
            Assert.AreEqual("Memory: alle 6 Paare in 41,0 s – am schnellsten +200", ergebnis.Vorschlaege[Bert].Grund);
            Assert.AreEqual(1, ergebnis.Vorschlaege.Count, "Wer nicht vorn ist, bekommt einen Vorschlag.");
        }

        [TestMethod]
        public void WithoutAFinisherTheMostPairsWinAndATieGivesAll()
        {
            var ergebnis = Werte(new()
            {
                [Anna] = new(3, null),
                [Bert] = new(3, null),
                [Carl] = new(2, null),
            });

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, ergebnis.Gewinner.ToList());
            Assert.AreEqual(Feld, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual("Memory: 3 von 6 Paaren – die meisten +200", ergebnis.Vorschlaege[Bert].Grund);
            Assert.AreEqual("2 von 6 Paaren", MemoryEvaluator.Describe(ergebnis, Carl));
        }

        [TestMethod]
        public void TheSameFinishTimeGivesAll()
        {
            var ergebnis = Werte(new() { [Anna] = Fertig(30), [Bert] = Fertig(30) });

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, ergebnis.Gewinner.ToList());
        }

        [TestMethod]
        public void NobodyWinsWithoutAPair()
        {
            var ergebnis = Werte(new() { [Anna] = new(0, null) });

            Assert.AreEqual(0, ergebnis.Gewinner.Count, "Ohne ein Paar gewinnt jemand.");
            Assert.AreEqual(0, ergebnis.Vorschlaege.Count);
            Assert.AreEqual("Keine Karte umgedreht – von Hand werten", MemoryEvaluator.Describe(ergebnis, Bert));
        }

        /// <summary>E2: ein Plus wird nie gekappt - auch nicht bei einem Kontostand unter 0 in einem Spiel ohne Minus.</summary>
        [TestMethod]
        public void APlusIsNeverCapped()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = -150 }, UnterNullErlaubt: false);

            var ergebnis = Werte(new() { [Anna] = Fertig(20) }, konten);

            Assert.AreEqual(Feld, ergebnis.Vorschlaege[Anna].Punkte, "Der Gewinn wurde am Kontostand gekappt.");
        }

        [TestMethod]
        public void TheBoardRanksByTimeThenPairsAndMarksTheWinner()
        {
            var ergebnis = Werte(new() { [Bert] = new(4, null), [Carl] = Fertig(42.34) });

            var zeilen = MemoryTafel.Ende(ergebnis, [(Anna, "Anna"), (Bert, "Bert"), (Carl, "Carl")]);

            CollectionAssert.AreEqual(new[] { "Carl", "Bert", "Anna" }, zeilen.Select(z => z.Name).ToList());
            Assert.AreEqual("✓ alle 6 Paare in 42,3 s", zeilen[0].Zeit);
            Assert.IsTrue(zeilen[0].Sieger);
            Assert.IsFalse(zeilen[1].Sieger);
            Assert.AreEqual("keine Karte umgedreht", zeilen[2].Zeit);

            var rennen = MemoryTafel.Rennen(new Dictionary<Guid, MemoryStand> { [Anna] = new(3, null) }, 6, [(Anna, "Anna"), (Bert, "Bert")]);
            Assert.AreEqual(0.5, rennen[0].BalkenAnteil, 1e-9, "Der Balken zeigt nicht den Anteil der Paare.");
            Assert.AreEqual("0 von 6 Paaren", rennen[1].Zeit);
        }
    }
}
