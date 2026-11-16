using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Minispiele;
using static Quizzer.DataModels.Questions.Minispiele.SchereSteinPapier;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Duell (M35 C2) als reine Funktion: Schere-Stein-Papier Best of n, Reaktion nur der beiden, Sieger
    /// +Anteil und Verlierer −Anteil als Vorschlag mit Grund, Unentschieden ohne Bewegung, Kappung (E2).
    /// </summary>
    [TestClass]
    public class DuellUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static string Name(Guid id) => id == Anna ? "Anna" : id == Bert ? "Bert" : "Carl";

        private static Dictionary<Guid, SchereSteinPapier> Runde(SchereSteinPapier? anna, SchereSteinPapier? bert)
        {
            var runde = new Dictionary<Guid, SchereSteinPapier>();
            if (anna is { } a) runde[Anna] = a;
            if (bert is { } b) runde[Bert] = b;
            return runde;
        }

        [TestMethod]
        [DataRow(Schere, Papier, 1)]
        [DataRow(Stein, Schere, 1)]
        [DataRow(Papier, Stein, 1)]
        [DataRow(Papier, Schere, -1)]
        [DataRow(Schere, Stein, -1)]
        [DataRow(Stein, Papier, -1)]
        [DataRow(Schere, Schere, 0)]
        [DataRow(Stein, Stein, 0)]
        [DataRow(Papier, Papier, 0)]
        public void EachMoveBeatsExactlyOneOther(SchereSteinPapier a, SchereSteinPapier b, int erwartet)
            => Assert.AreEqual(erwartet, DuellEvaluator.Vergleiche(a, b));

        [TestMethod]
        public void AMissingMoveLosesAgainstAMoveAndTwoMissingAreATie()
        {
            Assert.AreEqual(Anna, DuellEvaluator.RundenSieger(Anna, Bert, Runde(Stein, null)));
            Assert.AreEqual(Bert, DuellEvaluator.RundenSieger(Anna, Bert, Runde(null, Papier)));
            Assert.IsNull(DuellEvaluator.RundenSieger(Anna, Bert, Runde(null, null)));
            Assert.IsNull(DuellEvaluator.RundenSieger(Anna, Bert, Runde(Stein, Stein)));
        }

        [TestMethod]
        public void BestOfThreeCountsRoundWinsAndEndsAtTwo()
        {
            var nachZwei = DuellEvaluator.WerteSchereSteinPapier(Anna, Bert, [Runde(Stein, Schere), Runde(Papier, Stein)]);

            Assert.AreEqual(Anna, nachZwei.Sieger);
            Assert.AreEqual(Bert, nachZwei.Verlierer);
            Assert.AreEqual("2:0", nachZwei.Stand);
            Assert.IsTrue(DuellEvaluator.IstEntschieden(nachZwei, 3), "2:0 bei Best of 3 ist entschieden.");

            var einsEins = DuellEvaluator.WerteSchereSteinPapier(Anna, Bert, [Runde(Stein, Schere), Runde(Stein, Papier)]);
            Assert.IsFalse(DuellEvaluator.IstEntschieden(einsEins, 3), "1:1 bei Best of 3 ist offen.");
            Assert.IsNull(einsEins.Sieger, "Gleich viele Siege sind ein Unentschieden.");

            var mitGleichstand = DuellEvaluator.WerteSchereSteinPapier(Anna, Bert,
                [Runde(Stein, Stein), Runde(Stein, Papier), Runde(Schere, Schere)]);
            Assert.AreEqual(Bert, mitGleichstand.Sieger, "Eine Gleichstandsrunde zaehlt fuer niemanden.");
        }

        [TestMethod]
        public void TheReactionDuelCountsOnlyTheTwoDuellists()
        {
            var runde = new Dictionary<Guid, ReactionAttempt>
            {
                [Anna] = new(ReactionOutcome.Valid, 300),
                [Bert] = new(ReactionOutcome.Valid, 250),
                [Carl] = new(ReactionOutcome.Valid, 120),
            };

            var ergebnis = DuellEvaluator.WerteReaktion(Anna, Bert, [runde]);

            Assert.AreEqual(Bert, ergebnis.Sieger, "Ein Zuschauer mit schnellerer Zeit gewinnt das Duell.");

            var fruehstart = new Dictionary<Guid, ReactionAttempt>
            {
                [Anna] = new(ReactionOutcome.FalseStart, null),
                [Bert] = new(ReactionOutcome.FalseStart, null),
            };
            Assert.IsNull(DuellEvaluator.WerteReaktion(Anna, Bert, [fruehstart]).Sieger, "Ohne gueltige Zeit gibt es keinen Sieger.");

            var gleich = new Dictionary<Guid, ReactionAttempt> { [Anna] = new(ReactionOutcome.Valid, 250), [Bert] = new(ReactionOutcome.Valid, 250) };
            Assert.IsNull(DuellEvaluator.WerteReaktion(Anna, Bert, [gleich]).Sieger, "Gleiche Bestzeit ist ein Unentschieden.");
        }

        [TestMethod]
        public void TheWinnerGainsAndTheLoserLosesTheSameWithAReason()
        {
            var ergebnis = new DuellErgebnis(Anna, Bert, 2, 1, Anna);

            var vorschlaege = DuellEvaluator.Vorschlaege(ergebnis, 50, Kontostaende.OhneGrenze, Name);

            Assert.AreEqual(new Punktevorschlag(50, "Duell gegen Bert gewonnen"), vorschlaege[Anna]);
            Assert.AreEqual(new Punktevorschlag(-50, "Duell gegen Anna verloren"), vorschlaege[Bert]);
            Assert.AreEqual(2, vorschlaege.Count);
        }

        [TestMethod]
        public void ADrawMovesNothing()
        {
            var vorschlaege = DuellEvaluator.Vorschlaege(new DuellErgebnis(Anna, Bert, 1, 1, null), 50, Kontostaende.OhneGrenze, Name);
            Assert.AreEqual(0, vorschlaege.Count);
        }

        /// <summary>E2: nur der Verlierer wird gekappt - der Sieger bekommt seinen Anteil trotzdem.</summary>
        [TestMethod]
        public void TheLoserIsCappedOnlyWhenTheGameForbidsMinus()
        {
            var ergebnis = new DuellErgebnis(Anna, Bert, 2, 0, Anna);
            var stand = new Dictionary<Guid, int> { [Anna] = 0, [Bert] = 30 };

            var verboten = DuellEvaluator.Vorschlaege(ergebnis, 50, new Kontostaende(stand, UnterNullErlaubt: false), Name);
            Assert.AreEqual(-30, verboten[Bert].Punkte);
            StringAssert.Contains(verboten[Bert].Grund, "gekappt");
            Assert.AreEqual(50, verboten[Anna].Punkte);

            var erlaubt = DuellEvaluator.Vorschlaege(ergebnis, 50, new Kontostaende(stand, UnterNullErlaubt: true), Name);
            Assert.AreEqual(-50, erlaubt[Bert].Punkte);
            Assert.IsFalse(erlaubt[Bert].Grund.Contains("gekappt"));
        }

        [TestMethod]
        public void TheShareIsRoundedCommerciallyAndNeverNegative()
        {
            Assert.AreEqual(50, Punkteanteil.Von(100, 50));
            Assert.AreEqual(13, Punkteanteil.Von(25, 50), "12,5 rundet kaufmaennisch auf.");
            Assert.AreEqual(0, Punkteanteil.Von(-100, 50));
        }
    }
}
