using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Bestwert je Minispiel-Art (M35 C9): welche Art einen Rohwert fuehrt, sein Bereich und das Abgleichformat.
    /// </summary>
    [TestClass]
    public class BestwertUnitTests
    {
        /// <summary>
        /// Arten, deren Runde keinen vergleichbaren Messwert hat - Punkte, Wahlen oder ein Wert, der am Inhalt der Frage
        /// haengt (Memory: Zahl der Paare). Eine neue Art landet entweder hier oder bekommt einen Bestwert.
        /// </summary>
        private static readonly MinigameKind[] OhneBestwert =
        [
            MinigameKind.Majority,
            MinigameKind.Duel,
            MinigameKind.RobberAndGuard,
            MinigameKind.ShareOrTake,
            MinigameKind.WheelOfFortune,
            MinigameKind.Memory,
            MinigameKind.LightningRound,
            MinigameKind.WordScramble,
            MinigameKind.ExplainIt,
            MinigameKind.Bluff,
            MinigameKind.HotPotato,

            // M37 T9: gewonnen wird als Team; die Tipps je Person laufen an der Serveruhr samt Nachlauf - mit dem Tippsprint nicht vergleichbar.
            MinigameKind.TugOfWar,
        ];

        [TestMethod]
        public void EveryKindEitherHasARecordOrIsListedWithout()
        {
            foreach (var art in Enum.GetValues<MinigameKind>())
            {
                var hat = MinigameProfiles.For(art).Record != null;
                var ohne = OhneBestwert.Contains(art);

                Assert.IsTrue(hat ^ ohne, hat
                    ? $"{art} hat einen Bestwert und steht trotzdem auf der Liste ohne."
                    : $"{art} hat weder einen Bestwert noch steht sie auf der Liste ohne - neue Art bewusst entscheiden.");
            }
        }

        [TestMethod]
        public void AtLeastTheMeasuringKindsHaveARecord()
        {
            foreach (var art in new[] { MinigameKind.Reaction, MinigameKind.TapSprint, MinigameKind.Stopwatch, MinigameKind.MentalArithmetic })
                Assert.IsNotNull(MinigameProfiles.For(art).Record, $"{art} fuehrt keinen Bestwert.");
        }

        /// <summary>
        /// M37 E05: nur das Kopfrechnen trennt seine Liste nach Spielweise, und zwar je Schwierigkeit mit ihrem Namen. Eine
        /// weitere Art mit Unterteilung wird hier bewusst eingetragen.
        /// </summary>
        [TestMethod]
        public void OnlyMentalArithmeticSplitsItsListAndNamesEveryDifficulty()
        {
            var unterteilt = MinigameProfiles.All.Where(p => p.Record is { VariantTitles.Count: > 0 }).ToList();

            CollectionAssert.AreEqual(new[] { MinigameKind.MentalArithmetic }, unterteilt.Select(p => p.Kind).ToList());

            foreach (var profil in unterteilt)
                Assert.AreEqual(profil.Variants.Count, profil.Record!.VariantTitles.Count, $"{profil.Kind}: nicht jede Spielweise hat einen Titel.");

            var titel = MinigameProfiles.For(MinigameKind.MentalArithmetic).Record!.VariantTitles;
            Assert.AreEqual("leicht", titel[KopfrechnenAufgaben.Leicht]);
            Assert.AreEqual("mittel", titel[KopfrechnenAufgaben.Mittel]);
            Assert.AreEqual("schwer", titel[KopfrechnenAufgaben.Schwer]);
        }

        [TestMethod]
        public void TheReactionRangeIsWhatTheRoundCounts()
        {
            var record = MinigameProfiles.For(MinigameKind.Reaction).Record!;

            Assert.IsTrue(record.LowerIsBetter);
            Assert.IsFalse(record.IsPlausible(ReaktionEvaluator.FalseStartBelowMs - 1), "Ein Fruehstart waere ein Rekord.");
            Assert.IsTrue(record.IsPlausible(ReaktionEvaluator.FalseStartBelowMs));
            Assert.IsTrue(record.IsPlausible(ReaktionEvaluator.NoScoreAboveMs));
            Assert.IsFalse(record.IsPlausible(ReaktionEvaluator.NoScoreAboveMs + 1), "Eine ungewertete Zeit waere ein Rekord.");
            Assert.AreEqual("187 ms", record.Format(187));
        }

        [TestMethod]
        public void TheOtherRangesFollowTheirEvaluators()
        {
            var tipps = MinigameProfiles.For(MinigameKind.TapSprint).Record!;
            Assert.IsFalse(tipps.LowerIsBetter);
            Assert.AreEqual(TippsprintEvaluator.MaxTapsPerSecond * 100, tipps.Max, "Mehr als die Grenze je Sekunde nimmt der Buzzer nicht an.");
            Assert.AreEqual("7,35 Tipps/s", tipps.Format(735));

            var stoppuhr = MinigameProfiles.For(MinigameKind.Stopwatch).Record!;
            Assert.IsTrue(stoppuhr.LowerIsBetter);
            Assert.AreEqual(MinigameProfiles.For(MinigameKind.Stopwatch).MaxDurationSeconds * 1000, stoppuhr.Max,
                "Gewertet wird bis zur doppelten Zielzeit - der Abstand ist hoechstens die laengste Zielzeit.");
            Assert.AreEqual("genau getroffen", stoppuhr.Format(0));

            var rechnen = MinigameProfiles.For(MinigameKind.MentalArithmetic).Record!;
            Assert.AreEqual(KopfrechnenEvaluator.MaxAntwortenJeSekunde * 60 * 100, rechnen.Max);

            var zocker = MinigameProfiles.For(MinigameKind.Crash).Record!;
            Assert.AreEqual(ZockerEvaluator.Startwert, zocker.Min);
            Assert.AreEqual(ZockerEvaluator.Obergrenze, zocker.Max);
            Assert.AreEqual("×2,15", zocker.Format(215));
        }

        [TestMethod]
        public void TheRecordColumnsCameWithFormat24()
        {
            // Ueber eine Variable wie bei den Jokern - die Konstante direkt meldet MSTEST0032 („immer wahr").
            var format = SyncSnapshot.CurrentFormatVersion;
            Assert.IsTrue(format >= 24,
                "Rohwert und Klaus am Ergebnis ohne neues Abgleichformat - ein altes WPF setzte beim Push null und 0 ein.");
        }
    }
}
