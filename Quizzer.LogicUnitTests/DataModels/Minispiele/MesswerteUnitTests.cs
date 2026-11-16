using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Messwerte und Klaus fuer die Bestenliste (M35 C9): was eine Runde je Spieler als Rohwert liefert, und welche
    /// Punktebewegung als Klau zaehlt.
    /// </summary>
    [TestClass]
    public class MesswerteUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static string Name(Guid id) => id == Anna ? "Anna" : id == Bert ? "Bert" : "Carl";

        private static MinigameRecord Record(MinigameKind art) => MinigameProfiles.For(art).Record ?? throw new AssertFailedException($"{art} hat keinen Bestwert.");

        [TestMethod]
        public void TheReactionGivesEachPlayersBestValidTime()
        {
            var ergebnis = ReaktionEvaluator.Werte(
            [
                new Dictionary<Guid, ReactionAttempt> { [Anna] = new(ReactionOutcome.Valid, 300), [Bert] = new(ReactionOutcome.FalseStart, null) },
                new Dictionary<Guid, ReactionAttempt> { [Anna] = new(ReactionOutcome.Valid, 220), [Bert] = new(ReactionOutcome.TooSlow, 6000) },
            ]);

            var werte = Messwerte.Reaktion(ergebnis);

            Assert.AreEqual(220, werte[Anna], "Nicht die beste Einzelzeit.");
            Assert.IsFalse(werte.ContainsKey(Bert), "Fruehstart und zu langsam sind kein Messwert.");
        }

        [TestMethod]
        public void TheTapSprintCountsTapsPerSecondOfTheSetDuration()
        {
            var ergebnis = TippsprintEvaluator.Werte(new Dictionary<Guid, int> { [Anna] = 73, [Bert] = 7, [Carl] = 0 });

            Assert.AreEqual(730, Messwerte.Tippsprint(ergebnis, 10)[Anna]);
            Assert.AreEqual(233, Messwerte.Tippsprint(ergebnis, 3)[Bert], "7 in 3 s sind 2,33 je Sekunde.");
            Assert.IsFalse(Messwerte.Tippsprint(ergebnis, 10).ContainsKey(Carl), "Null Tipps sind kein Rekord.");
            Assert.IsTrue(Record(MinigameKind.TapSprint).IsPlausible(
                Messwerte.Hundertstel(TippsprintEvaluator.MaxTapsPerSecond * 60, 60)), "Das Hoechste, was der Buzzer annimmt, ist nicht plausibel.");
        }

        [TestMethod]
        public void TheStopwatchGivesTheDistanceOfCountedTimesOnly()
        {
            var ergebnis = StoppuhrEvaluator.Werte(new Dictionary<Guid, StoppuhrVersuch>
            {
                [Anna] = new StoppuhrVersuch(10_230, true),
                [Bert] = new StoppuhrVersuch(9_900, true),
                [Carl] = new StoppuhrVersuch(25_000, false),
            }, 10);

            var werte = Messwerte.Stoppuhr(ergebnis);

            Assert.AreEqual(230, werte[Anna]);
            Assert.AreEqual(100, werte[Bert], "Zu frueh gestoppt ist ein Abstand wie zu spaet.");
            Assert.IsFalse(werte.ContainsKey(Carl), "Ueber der doppelten Zielzeit gibt es keinen Messwert.");
        }

        [TestMethod]
        public void MentalArithmeticCountsRightAnswersPerMinute()
        {
            var staende = new Dictionary<Guid, KopfrechnenStand> { [Anna] = new(12, 14), [Bert] = new(0, 3) };
            var ergebnis = KopfrechnenEvaluator.Werte(staende, 100, Kontostaende.OhneGrenze);

            Assert.AreEqual(1200, Messwerte.Kopfrechnen(ergebnis, 60)[Anna]);
            Assert.AreEqual(2400, Messwerte.Kopfrechnen(ergebnis, 30)[Anna], "Dieselbe Zahl in der halben Zeit ist doppelt so schnell.");
            Assert.IsFalse(Messwerte.Kopfrechnen(ergebnis, 60).ContainsKey(Bert), "Null richtige sind kein Rekord.");
        }

        [TestMethod]
        public void TheCrashGivesTheCashOutOfThoseWhoGotOut()
        {
            var ergebnis = ZockerEvaluator.Werte([Anna, Bert], new Dictionary<Guid, int> { [Anna] = 215 }, 300,
                new ZockerWertung(100, null, Kontostaende.OhneGrenze));

            var werte = Messwerte.Zocker(ergebnis);

            Assert.AreEqual(215, werte[Anna]);
            Assert.IsFalse(werte.ContainsKey(Bert), "Wer nicht ausstieg, hat keinen Ausstieg.");
        }

        [TestMethod]
        public void ASuccessfulRobberyIsASteal_ABlockedOneIsNot()
        {
            var ergebnis = RaeuberEvaluator.Werte(
                new Dictionary<Guid, RaeuberWahl> { [Anna] = RaeuberWahl.Raub(Bert), [Bert] = RaeuberWahl.Raub(Carl), [Carl] = RaeuberWahl.MitSchild },
                50, Kontostaende.OhneGrenze, Name);

            Assert.AreEqual(1, ergebnis.Vorschlaege[Anna].Klaus, "Der gelungene Raub zaehlt nicht als Klau.");
            Assert.AreEqual(0, ergebnis.Vorschlaege[Bert].Klaus, "Am Schild gescheitert zaehlt als Klau.");
            Assert.AreEqual(0, ergebnis.Vorschlaege[Carl].Klaus, "Das Schild zaehlt als Klau.");
        }

        [TestMethod]
        public void OnlyStealingAtTheWheelIsASteal()
        {
            var alle = new List<(Guid Id, string Name)> { (Anna, "Anna"), (Bert, "Bert") };
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = 0, [Bert] = 100 }, true);
            GluecksradErgebnis Dreh(WheelSegment feld) => GluecksradEvaluator.Werte(new(feld, Anna, Bert), 50, konten, alle);

            Assert.AreEqual(1, Dreh(WheelSegment.Steal).Vorschlaege[Anna].Klaus, "Klauen zaehlt nicht beim Dreher.");
            Assert.AreEqual(0, Dreh(WheelSegment.Steal).Vorschlaege[Bert].Klaus, "Das Opfer bekommt einen Klau.");
            Assert.IsTrue(Dreh(WheelSegment.Gift).Vorschlaege.Values.All(v => v.Klaus == 0), "Verschenken zaehlt als Klau.");
            Assert.IsTrue(Dreh(WheelSegment.RobinHood).Vorschlaege.Values.All(v => v.Klaus == 0), "Robin Hood zaehlt als Klau.");
        }

        [TestMethod]
        public void TheBountyIsAStealPerWantedPlayerItWasCollectedFrom()
        {
            static SonderregelEingang E(RatingKind bewertung, int grundwert) => new(bewertung, grundwert, 100, new Kappung(500, true));
            var eingaenge = new Dictionary<Guid, SonderregelEingang>
            {
                [Anna] = E(RatingKind.Wrong, -30), [Bert] = E(RatingKind.Wrong, -30), [Carl] = E(RatingKind.Right, 100),
            };

            var v = KopfgeldRegel.Werte([Anna, Bert], eingaenge, 20, Name);

            Assert.AreEqual(2, v[Carl].Klaus, "Der Jaeger kassiert bei zwei Gesuchten.");
            Assert.AreEqual(0, v[Anna].Klaus, "Der Gesuchte bekommt einen Klau.");
        }
    }
}
