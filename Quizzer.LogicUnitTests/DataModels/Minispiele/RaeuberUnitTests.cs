using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Raeuber und Waechter (M35 C2) als reine Funktion: Raub ohne Schild, Raub auf ein Schild, mehrere
    /// Raeuber beim selben Opfer, und die Kappung (E2) einmal auf die Summe je Spieler.
    /// </summary>
    [TestClass]
    public class RaeuberUnitTests
    {
        private const int Beute = 50;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static string Name(Guid id) => id == Anna ? "Anna" : id == Bert ? "Bert" : "Carl";

        private static RaeuberErgebnis Werte(Dictionary<Guid, RaeuberWahl> wahlen, Kontostaende? konten = null)
            => RaeuberEvaluator.Werte(wahlen, Beute, konten ?? Kontostaende.OhneGrenze, Name);

        [TestMethod]
        public void ARobberyWithoutShieldMovesTheLootFromVictimToThief()
        {
            var ergebnis = Werte(new() { [Anna] = RaeuberWahl.Raub(Bert), [Bert] = RaeuberWahl.Raub(Carl) });

            Assert.AreEqual(Beute, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(0, ergebnis.Vorschlaege[Bert].Punkte, "Bert raubt und wird beraubt - netto null.");
            Assert.AreEqual(-Beute, ergebnis.Vorschlaege[Carl].Punkte);
            StringAssert.Contains(ergebnis.Vorschlaege[Anna].Grund, "Bert bestohlen");
            StringAssert.Contains(ergebnis.Vorschlaege[Carl].Grund, "von Bert bestohlen");
        }

        [TestMethod]
        public void ARobberyOnAShieldMakesTheThiefPayTheVictim()
        {
            var ergebnis = Werte(new() { [Anna] = RaeuberWahl.Raub(Bert), [Bert] = RaeuberWahl.MitSchild });

            Assert.AreEqual(-Beute, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(Beute, ergebnis.Vorschlaege[Bert].Punkte);
            Assert.IsTrue(ergebnis.Zuege.Single().Abgewehrt);
            StringAssert.Contains(ergebnis.Vorschlaege[Anna].Grund, "am Schild von Bert gescheitert");
            Assert.AreEqual("Schild – hielt gegen Anna", RaeuberEvaluator.Describe(ergebnis, Bert, Name));
        }

        [TestMethod]
        public void SeveralRobbersEachTakeTheirShare()
        {
            var ergebnis = Werte(new() { [Anna] = RaeuberWahl.Raub(Carl), [Bert] = RaeuberWahl.Raub(Carl), [Carl] = RaeuberWahl.Raub(Anna) });

            Assert.AreEqual(Beute - Beute, ergebnis.Vorschlaege[Anna].Punkte, "Anna raubt Carl und wird von Carl beraubt.");
            Assert.AreEqual(Beute, ergebnis.Vorschlaege[Bert].Punkte);
            Assert.AreEqual(-2 * Beute + Beute, ergebnis.Vorschlaege[Carl].Punkte, "Carl verliert zweimal und gewinnt einmal.");
            Assert.AreEqual(3, ergebnis.Zuege.Count);
        }

        /// <summary>
        /// Die Entscheidung (M35 C2): erst die Summe je Spieler, dann einmal am Kontostand kappen - die Wahlen
        /// fallen gleichzeitig, eine Reihenfolge der Raube gibt es nicht. Carls eigene Beute zaehlt also mit;
        /// die Diebe behalten ihre (ein Plus wird nie gekappt).
        /// </summary>
        [TestMethod]
        public void TheVictimIsCappedOnceAtTheBalanceNotPerRobber()
        {
            var wahlen = new Dictionary<Guid, RaeuberWahl> { [Anna] = RaeuberWahl.Raub(Carl), [Bert] = RaeuberWahl.Raub(Carl), [Carl] = RaeuberWahl.Raub(Anna) };
            var stand = new Dictionary<Guid, int> { [Anna] = 0, [Bert] = 0, [Carl] = 30 };

            var verboten = Werte(wahlen, new Kontostaende(stand, UnterNullErlaubt: false));

            Assert.AreEqual(-30, verboten.Vorschlaege[Carl].Punkte, "Netto −50 wird einmal bei 30 gekappt.");
            StringAssert.Contains(verboten.Vorschlaege[Carl].Grund, "gekappt");
            Assert.AreEqual(Beute, verboten.Vorschlaege[Bert].Punkte, "Der Dieb bekommt seine Beute trotz Kappung beim Opfer.");
            Assert.AreEqual(0, verboten.Vorschlaege[Anna].Punkte, "Anna: +50 von Carl, −50 an Carl - netto null, nichts zu kappen.");

            var erlaubt = Werte(wahlen, new Kontostaende(stand, UnterNullErlaubt: true));
            Assert.AreEqual(-50, erlaubt.Vorschlaege[Carl].Punkte);
            Assert.IsFalse(erlaubt.Vorschlaege[Carl].Grund.Contains("gekappt"));
        }

        [TestMethod]
        public void ASumBelowZeroIsCappedAgainstEveryRobberTogether()
        {
            var wahlen = new Dictionary<Guid, RaeuberWahl> { [Anna] = RaeuberWahl.Raub(Carl), [Bert] = RaeuberWahl.Raub(Carl) };
            var stand = new Dictionary<Guid, int> { [Carl] = 60 };

            var ergebnis = Werte(wahlen, new Kontostaende(stand, UnterNullErlaubt: false));

            Assert.AreEqual(-60, ergebnis.Vorschlaege[Carl].Punkte, "Zwei Raeuber zu je 50 bei 60: Carl faellt nur bis 0.");
        }

        [TestMethod]
        public void AThiefWhoHitsAShieldIsCappedToo()
        {
            var stand = new Dictionary<Guid, int> { [Anna] = 10 };
            var ergebnis = Werte(new() { [Anna] = RaeuberWahl.Raub(Bert), [Bert] = RaeuberWahl.MitSchild }, new Kontostaende(stand, false));

            Assert.AreEqual(-10, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(Beute, ergebnis.Vorschlaege[Bert].Punkte, "Das Schild bekommt die volle Beute.");
        }

        [TestMethod]
        public void RobbingYourselfAndAShieldNobodyAttacksMoveNothing()
        {
            var ergebnis = Werte(new() { [Anna] = RaeuberWahl.Raub(Anna), [Bert] = RaeuberWahl.MitSchild });

            Assert.AreEqual(0, ergebnis.Vorschlaege.Count);
            Assert.AreEqual(0, ergebnis.Zuege.Count);
            Assert.AreEqual("Schild – niemand kam", RaeuberEvaluator.Describe(ergebnis, Bert, Name));
            Assert.AreEqual("keine Abgabe", RaeuberEvaluator.Describe(ergebnis, Carl, Name));
        }
    }
}
