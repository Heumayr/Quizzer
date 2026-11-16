using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Das Gluecksrad (M35 C4) als reine Funktion: jedes Feld, die Kappung (E2) nur fuer ein Minus, und
    /// Robin Hood bei Gleichstand.
    /// </summary>
    [TestClass]
    public class GluecksradUnitTests
    {
        private const int Anteil = 100;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();

        private static readonly List<(Guid Id, string Name)> Alle =
            [(Anna, "Anna"), (Bert, "Bert"), (Carl, "Carl"), (Dora, "Dora")];

        private static GluecksradErgebnis Werte(WheelSegment feld, Guid? ziel = null, Kontostaende? konten = null, Guid? dreher = null)
            => GluecksradEvaluator.Werte(new(feld, dreher ?? Anna, ziel), Anteil, konten ?? Kontostaende.OhneGrenze, Alle);

        private static Kontostaende Stand(int anna, int bert, int carl, int dora, bool unterNull = true)
            => new(new Dictionary<Guid, int> { [Anna] = anna, [Bert] = bert, [Carl] = carl, [Dora] = dora }, unterNull);

        [TestMethod]
        public void DoubleAndBadLuckMoveOnlyTheSpinner()
        {
            var doppelt = Werte(WheelSegment.Double);
            Assert.AreEqual(2 * Anteil, doppelt.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(1, doppelt.Vorschlaege.Count);
            StringAssert.Contains(doppelt.Vorschlaege[Anna].Grund, "Glücksrad (Verdoppeln)");

            var pech = Werte(WheelSegment.BadLuck);
            Assert.AreEqual(-Anteil, pech.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(1, pech.Vorschlaege.Count);
        }

        [TestMethod]
        public void StealAndGiftMoveTheShareBetweenSpinnerAndTarget()
        {
            var klau = Werte(WheelSegment.Steal, Bert);
            Assert.AreEqual(Anteil, klau.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(-Anteil, klau.Vorschlaege[Bert].Punkte);
            Assert.AreEqual("beklaut Bert", klau.Describe(Anna));
            Assert.AreEqual("von Anna beklaut", klau.Describe(Bert));
            Assert.AreEqual("schaut beim Glücksrad zu", klau.Describe(Carl));

            var geschenk = Werte(WheelSegment.Gift, Bert);
            Assert.AreEqual(-Anteil, geschenk.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(Anteil, geschenk.Vorschlaege[Bert].Punkte);
        }

        [TestMethod]
        public void WithoutTargetOrSpinnerNothingMoves()
        {
            Assert.AreEqual(0, Werte(WheelSegment.Steal).Vorschlaege.Count, "Klauen ohne Ziel bewegt etwas.");
            Assert.AreEqual(0, Werte(WheelSegment.Gift, ziel: Anna).Vorschlaege.Count, "Sich selbst beschenken bewegt etwas.");
            Assert.AreEqual(0, GluecksradEvaluator.Werte(new(WheelSegment.Double, null, null), Anteil, Kontostaende.OhneGrenze, Alle).Vorschlaege.Count,
                "Ohne Dreher bekommt jemand den doppelten Anteil.");
            Assert.AreEqual("Klauen – es wurde kein Ziel gewählt.", GluecksradEvaluator.Ausgang(new(WheelSegment.Steal, Anna, null), _ => "Anna"));
        }

        /// <summary>E2: das Minus des Opfers bzw. des Schenkenden wird gekappt, das Plus des anderen nie.</summary>
        [TestMethod]
        public void OnlyTheMinusIsCappedAtTheBalance()
        {
            var konten = Stand(anna: 30, bert: 20, carl: 0, dora: 0, unterNull: false);

            var klau = Werte(WheelSegment.Steal, Bert, konten);
            Assert.AreEqual(Anteil, klau.Vorschlaege[Anna].Punkte, "Die Beute wurde gekappt.");
            Assert.AreEqual(-20, klau.Vorschlaege[Bert].Punkte);
            StringAssert.Contains(klau.Vorschlaege[Bert].Grund, Punkteanteil.GekapptText);

            var geschenk = Werte(WheelSegment.Gift, Bert, konten);
            Assert.AreEqual(-30, geschenk.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(Anteil, geschenk.Vorschlaege[Bert].Punkte, "Das Geschenk wurde gekappt.");

            Assert.AreEqual(-30, Werte(WheelSegment.BadLuck, konten: konten).Vorschlaege[Anna].Punkte);
        }

        [TestMethod]
        public void RobinHoodTakesFromTheLeaderAndGivesTheLast()
        {
            var ergebnis = Werte(WheelSegment.RobinHood, konten: Stand(anna: 50, bert: 400, carl: 100, dora: 200), dreher: Carl);

            Assert.AreEqual(2, ergebnis.Vorschlaege.Count, "Robin Hood trifft nicht genau Fuehrenden und Letzten.");
            Assert.AreEqual(-Anteil, ergebnis.Vorschlaege[Bert].Punkte);
            Assert.AreEqual(Anteil, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.IsFalse(ergebnis.Vorschlaege.ContainsKey(Carl), "Der Dreher ist bei Robin Hood betroffen.");
        }

        /// <summary>Gleichstand: die Fuehrenden geben B zusammen, die Letzten bekommen B zusammen; alle gleich - nichts.</summary>
        [TestMethod]
        public void RobinHoodSplitsTheShareOnATieAndDoesNothingWhenAllAreEqual()
        {
            var ergebnis = Werte(WheelSegment.RobinHood, konten: Stand(anna: 300, bert: 300, carl: 0, dora: 100));

            Assert.AreEqual(-50, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(-50, ergebnis.Vorschlaege[Bert].Punkte);
            Assert.AreEqual(Anteil, ergebnis.Vorschlaege[Carl].Punkte);
            Assert.IsFalse(ergebnis.Vorschlaege.ContainsKey(Dora));

            Assert.AreEqual(0, Werte(WheelSegment.RobinHood, konten: Stand(7, 7, 7, 7)).Vorschlaege.Count, "Alle gleich - und doch bewegt sich etwas.");
        }

        [TestMethod]
        public void TheWheelHasFiveEqualFieldsAndATargetOnlyForStealAndGift()
        {
            CollectionAssert.AreEquivalent(Enum.GetValues<WheelSegment>(), GluecksradEvaluator.Felder.ToList(),
                "Nicht jedes Feld steht genau einmal am Rad.");
            Assert.AreEqual(WheelSegment.Double, GluecksradEvaluator.Segment(-3));
            Assert.AreEqual(WheelSegment.RobinHood, GluecksradEvaluator.Segment(99));

            var mitZiel = Enum.GetValues<WheelSegment>().Where(GluecksradEvaluator.BrauchtZiel).ToList();
            CollectionAssert.AreEquivalent(new[] { WheelSegment.Steal, WheelSegment.Gift }, mitZiel);

            var profil = MinigameProfiles.For(MinigameKind.WheelOfFortune);
            Assert.IsTrue(profil.ChoosesPartner, "Die Paarwahl des Spielleiters fehlt beim Glücksrad.");
            Assert.AreEqual("Wer dreht", profil.ChooserLabel);
            Assert.AreEqual(2, profil.MinPlayers);
        }
    }
}
