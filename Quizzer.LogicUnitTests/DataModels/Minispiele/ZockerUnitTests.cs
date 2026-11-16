using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Der Zocker (M35 C4) als reine Funktion: Kurve und Absturz passen zusammen, die Verteilung ist fair,
    /// und die Wertung - ohne und mit Einsatz, mit Kappung (E2) und Gleichstand.
    /// </summary>
    [TestClass]
    public class ZockerUnitTests
    {
        private const int Feld = 200;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();
        private static readonly Guid[] Alle = [Anna, Bert, Carl];

        private static ZockerErgebnis Werte(
            Dictionary<Guid, int> ausstiege, int? einsatz = null, Kontostaende? konten = null, Guid[]? teilnehmer = null)
            => ZockerEvaluator.Werte(teilnehmer ?? Alle, ausstiege, 240, new ZockerWertung(Feld, einsatz, konten ?? Kontostaende.OhneGrenze));

        [TestMethod]
        public void TheMultiplierStartsAtOneAndRisesWithTime()
        {
            Assert.AreEqual(100, ZockerEvaluator.Multiplikator(TimeSpan.FromSeconds(-2)), "Im Vorlauf steht schon ein Wert ueber ×1.");
            Assert.AreEqual(100, ZockerEvaluator.Multiplikator(TimeSpan.Zero));
            Assert.AreEqual(271, ZockerEvaluator.Multiplikator(ZockerEvaluator.Zeitkonstante), "e nach einer Zeitkonstante.");

            var vorher = 0;
            for (var ms = 0; ms <= 20_000; ms += 100)
            {
                var jetzt = ZockerEvaluator.Multiplikator(TimeSpan.FromMilliseconds(ms));
                Assert.IsTrue(jetzt >= vorher, $"Bei {ms} ms faellt der Multiplikator.");
                vorher = jetzt;
            }

            Assert.AreEqual(ZockerEvaluator.Obergrenze, vorher, "Ueber der Obergrenze waechst er weiter.");
        }

        /// <summary>Vor der Absturzzeit liegt jeder Wert darunter, ab ihr steht er - sonst stiege man „bei" ×2,40 aus und stuerzte bei ×2,40 ab.</summary>
        [TestMethod]
        public void BeforeTheCrashTimeEveryMultiplierIsBelowTheCrashPoint()
        {
            foreach (var absturz in new[] { 101, 150, 199, 200, 240, 555, 999, 1000 })
            {
                var zeit = ZockerEvaluator.Absturzzeit(absturz);

                Assert.IsTrue(ZockerEvaluator.Multiplikator(zeit - TimeSpan.FromMilliseconds(1)) < absturz,
                    $"Kurz vor dem Absturz bei {absturz} steht schon der Absturzpunkt.");
                Assert.IsTrue(ZockerEvaluator.Multiplikator(zeit + TimeSpan.FromMilliseconds(1)) >= absturz - 1,
                    $"Die Absturzzeit fuer {absturz} liegt zu frueh.");
            }

            Assert.AreEqual(TimeSpan.Zero, ZockerEvaluator.Absturzzeit(100), "×1,00 stuerzt sofort ab.");
        }

        /// <summary>P(Absturz ≥ m) = 1/m - gezaehlt ueber alle Wuerfe, nicht behauptet.</summary>
        [TestMethod]
        public void TheCrashDistributionIsFairUpToTheCap()
        {
            var punkte = Enumerable.Range(0, ZockerEvaluator.Wurfbereich).Select(ZockerEvaluator.Absturz).ToList();

            Assert.AreEqual(100, punkte.Min());
            Assert.AreEqual(ZockerEvaluator.Obergrenze, punkte.Max());

            foreach (var m in new[] { 150, 200, 300, 500, 1000 })
            {
                var anteil = punkte.Count(p => p >= m) / (double)punkte.Count;
                Assert.AreEqual(100.0 / m, anteil, 0.005, $"P(Absturz ≥ {m}) ist nicht 1/{m / 100.0}.");
            }

            Assert.AreEqual(100, ZockerEvaluator.Absturz(-5), "Ein Wurf unter dem Bereich faellt nicht an den Rand.");
            Assert.AreEqual(1000, ZockerEvaluator.Absturz(99_999), "Ein Wurf ueber dem Bereich faellt nicht an den Rand.");
        }

        [TestMethod]
        public void WithoutStakeTheCashOutBringsFieldTimesMultiplierAndTheCrashNothing()
        {
            var ergebnis = Werte(new() { [Anna] = 185, [Bert] = 120 });

            Assert.AreEqual(370, ergebnis.Vorschlaege[Anna].Punkte, "200 × 1,85");
            Assert.AreEqual(240, ergebnis.Vorschlaege[Bert].Punkte, "200 × 1,20");
            Assert.IsFalse(ergebnis.Vorschlaege.ContainsKey(Carl), "Ohne Einsatz kostet der Absturz etwas.");
            StringAssert.Contains(ergebnis.Vorschlaege[Anna].Grund, "bei ×1,85 ausgestiegen");
            Assert.AreEqual("abgestürzt bei ×2,40", ZockerEvaluator.Describe(ergebnis, Carl));
            Assert.IsTrue(ergebnis.IstGewinner(Anna));
            Assert.IsFalse(ergebnis.IstGewinner(Bert));
        }

        [TestMethod]
        public void WithStakeTheCashOutWinsStakeTimesMultiplierMinusOneAndTheCrashCostsTheStake()
        {
            var ergebnis = Werte(new() { [Anna] = 185, [Bert] = 100 }, einsatz: 100);

            Assert.AreEqual(85, ergebnis.Vorschlaege[Anna].Punkte, "100 × (1,85 − 1)");
            Assert.IsFalse(ergebnis.Vorschlaege.ContainsKey(Bert), "Ausstieg bei ×1,00 bringt nichts - kein Vorschlag.");
            Assert.AreEqual(-100, ergebnis.Vorschlaege[Carl].Punkte);
            StringAssert.Contains(ergebnis.Vorschlaege[Carl].Grund, "Einsatz verloren");
            StringAssert.Contains(ergebnis.Vorschlaege[Carl].Grund, "×2,40");
        }

        /// <summary>E2: ein Absturz kappt am Kontostand; ein Gewinn wird nie gekappt.</summary>
        [TestMethod]
        public void TheLostStakeIsCappedAtTheBalanceButAWinNever()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = 0, [Carl] = 30 }, UnterNullErlaubt: false);

            var ergebnis = Werte(new() { [Anna] = 300 }, einsatz: 100, konten);

            Assert.AreEqual(200, ergebnis.Vorschlaege[Anna].Punkte, "Ein Gewinn wurde gekappt.");
            Assert.AreEqual(-30, ergebnis.Vorschlaege[Carl].Punkte, "Der Einsatz faellt unter den Kontostand.");
            StringAssert.Contains(ergebnis.Vorschlaege[Carl].Grund, Punkteanteil.GekapptText);
            Assert.AreEqual(0, ergebnis.Vorschlaege[Bert].Punkte, "Bei Kontostand 0 bleibt ein Minus stehen.");
            StringAssert.Contains(ergebnis.Vorschlaege[Bert].Grund, Punkteanteil.GekapptText, "Warum es 0 ist, steht nicht da.");
        }

        [TestMethod]
        public void OnlyParticipantsCountAndATieHasSeveralWinners()
        {
            var ergebnis = Werte(new() { [Anna] = 150, [Bert] = 150, [Carl] = 500 }, teilnehmer: [Anna, Bert]);

            Assert.IsFalse(ergebnis.Vorschlaege.ContainsKey(Carl), "Wer beim Start fehlte, bekommt etwas.");
            Assert.AreEqual("nicht dabei", ZockerEvaluator.Describe(ergebnis, Carl));
            Assert.IsTrue(ergebnis.IstGewinner(Anna) && ergebnis.IstGewinner(Bert), "Bei Gleichstand gewinnen nicht beide.");
        }

        [TestMethod]
        public void TheTextUsesTheGermanDecimalComma()
        {
            Assert.AreEqual("×1,85", ZockerEvaluator.Text(185));
            Assert.AreEqual("×10,00", ZockerEvaluator.Text(1000));
        }

        [TestMethod]
        public void TheProfileOffersTheStakeAsSecondVariantAndReadsTheShare()
        {
            var profil = MinigameProfiles.For(MinigameKind.Crash);

            Assert.AreEqual("Mit Einsatz", profil.Variants[ZockerEvaluator.MitEinsatzVariant].Name,
                "Die Spielweise mit Einsatz steht nicht dort, wo der Evaluator sie erwartet.");
            Assert.AreEqual(BuzzerControlsLayout.Minigame, profil.Layout);
            Assert.IsTrue(profil.ShowShare);
            Assert.IsFalse(profil.ShowRounds, "Der Zocker spielt genau eine Runde.");
            StringAssert.Contains(MinigameProfiles.RulesFor(new() { Kind = MinigameKind.Crash, Variant = 1, Share = 40 }), "40 %");
        }
    }
}
