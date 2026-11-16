using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Die Wertung der Mehrheit (M35 B2): groesste Gruppe, Gleichstand, Minderheit, keine Abgabe.
    /// Rein, ohne Oberflaeche - die Spielerkarte uebernimmt nur, was hier herauskommt.
    /// </summary>
    [TestClass]
    public class MehrheitEvaluatorUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Ben = Guid.NewGuid();
        private static readonly Guid Cleo = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();
        private static readonly Guid Emil = Guid.NewGuid();

        private static readonly string[] Optionen = ["A", "B", "C"];

        private static MehrheitErgebnis Werte(bool minderheit, params (Guid Wer, string? Taste)[] abgaben)
            => MehrheitEvaluator.Werte(abgaben.ToDictionary(a => a.Wer, a => a.Taste), Optionen, minderheit);

        /// <summary>Die groesste Gruppe gewinnt - und nur sie.</summary>
        [TestMethod]
        public void TheLargestGroupWins()
        {
            var e = Werte(false, (Anna, "A"), (Ben, "A"), (Cleo, "B"));

            Assert.IsTrue(e.IstGewinner(Anna));
            Assert.IsTrue(e.IstGewinner(Ben));
            Assert.IsFalse(e.IstGewinner(Cleo), "Die kleinere Gruppe bekommt einen Vorschlag.");
            CollectionAssert.AreEquivalent(new[] { "A" }, e.GewinnerTasten.ToList());
            Assert.AreEqual(2, e.GruppeVon(Anna));
        }

        /// <summary>Gleichstand: alle groessten Gruppen gewinnen, die kleinere nicht.</summary>
        [TestMethod]
        public void ATieLetsEveryLargestGroupWin()
        {
            var e = Werte(false, (Anna, "A"), (Ben, "A"), (Cleo, "B"), (Dora, "B"), (Emil, "C"));

            CollectionAssert.AreEquivalent(new[] { "A", "B" }, e.GewinnerTasten.ToList());
            Assert.IsTrue(e.IstGewinner(Anna) && e.IstGewinner(Cleo));
            Assert.IsFalse(e.IstGewinner(Emil));
        }

        /// <summary>
        /// Minderheit: die kleinste <b>nicht leere</b> Gruppe gewinnt. Option C waehlt niemand - waere
        /// sie mitgezaehlt, gewoenne eine leere Gruppe und niemand bekaeme Punkte.
        /// </summary>
        [TestMethod]
        public void InTheMinorityVariantTheSmallestChosenGroupWins()
        {
            var e = Werte(true, (Anna, "A"), (Ben, "A"), (Cleo, "B"));

            CollectionAssert.AreEquivalent(new[] { "B" }, e.GewinnerTasten.ToList());
            Assert.IsTrue(e.IstGewinner(Cleo));
            Assert.IsFalse(e.IstGewinner(Anna));
        }

        /// <summary>Minderheit mit Gleichstand: alle kleinsten Gruppen gewinnen.</summary>
        [TestMethod]
        public void AMinorityTieLetsEverySmallestGroupWin()
        {
            var e = Werte(true, (Anna, "A"), (Ben, "A"), (Cleo, "B"), (Dora, "C"));

            CollectionAssert.AreEquivalent(new[] { "B", "C" }, e.GewinnerTasten.ToList());
        }

        /// <summary>
        /// Wer nicht abgegeben hat, bekommt keinen Vorschlag - und zaehlt auch nicht als eigene Gruppe.
        /// Sonst gewoenne in der Minderheit die „Gruppe" der Nichtwaehler.
        /// </summary>
        [TestMethod]
        public void WhoeverDidNotChooseGetsNothingAndCountsForNothing()
        {
            var e = Werte(true, (Anna, "A"), (Ben, "A"), (Cleo, null), (Dora, ""));

            Assert.IsFalse(e.HatGewaehlt(Cleo));
            Assert.IsFalse(e.IstGewinner(Cleo));
            Assert.IsFalse(e.IstGewinner(Dora));
            Assert.AreEqual(2, e.Abgaben.Count);
            CollectionAssert.AreEquivalent(new[] { "A" }, e.GewinnerTasten.ToList());
        }

        /// <summary>
        /// Dasselbe <b>ohne Tastenliste</b>. Mit ihr hielte der Tastenfilter die leere Abgabe schon
        /// fest - die Mutationsprobe am Leer-Riegel blieb dort gruen (gemessen M35 B2).
        /// </summary>
        [TestMethod]
        public void WithoutAKeyListAnEmptyVoteIsStillNoVote()
        {
            var e = MehrheitEvaluator.Werte(
                new Dictionary<Guid, string?> { [Anna] = "A", [Ben] = "A", [Cleo] = null, [Dora] = "" },
                null, minderheit: true);

            Assert.IsFalse(e.HatGewaehlt(Cleo));
            Assert.IsFalse(e.IstGewinner(Dora), "In der Minderheit gewinnt die Gruppe der Nichtwaehler.");
            CollectionAssert.AreEquivalent(new[] { "A" }, e.GewinnerTasten.ToList());
        }

        /// <summary>Eine Taste, die zu keiner Option gehoert, kommt aus dem WLAN und zaehlt nicht.</summary>
        [TestMethod]
        public void AnUnknownKeyIsNotAVote()
        {
            var e = Werte(false, (Anna, "Z"), (Ben, "B"));

            Assert.IsFalse(e.HatGewaehlt(Anna));
            Assert.IsTrue(e.IstGewinner(Ben));
        }

        /// <summary>Hat niemand gewaehlt, gewinnt niemand - auch keine leere Option.</summary>
        [TestMethod]
        public void WithoutAnyVoteNobodyWins()
        {
            var e = Werte(false, (Anna, null), (Ben, null));

            Assert.AreEqual(0, e.GewinnerTasten.Count);
            Assert.AreEqual(0, e.Stimmen.Count);
        }
    }
}
