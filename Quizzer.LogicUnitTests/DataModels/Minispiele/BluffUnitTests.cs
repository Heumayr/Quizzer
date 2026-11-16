using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Der Bluff (M35 C6) als reine Funktion: die echte finden bringt die Feldpunkte, jeder Reinfall auf den eigenen Bluff
    /// die Haelfte; gestrichene Bluffs bringen nichts, zusammengefasste zaehlen fuer jeden Urheber; Gleichstand, niemand
    /// richtig, und ein Plus wird nie gekappt.
    /// </summary>
    [TestClass]
    public class BluffUnitTests
    {
        private const int Feld = 300;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();
        private static readonly Guid Dora = Guid.NewGuid();

        private static readonly (Guid, string)[] Spieler = [(Anna, "Anna"), (Bert, "Bert"), (Carl, "Carl"), (Dora, "Dora")];

        /// <summary>1 = echt „Rom", 2 = Annas „Paris", 3 = Berts „Wien", 0 = Carls gestrichenes „rom".</summary>
        private static List<BluffOption> Optionen() =>
        [
            new(1, "Rom", true, []),
            new(2, "Paris", false, [Anna]),
            new(3, "Wien", false, [Bert]),
            new(0, "rom", false, [Carl], Gestrichen: true),
        ];

        private static BluffErgebnis Werte(Dictionary<Guid, int> wahl, List<BluffOption>? optionen = null, Kontostaende? konten = null)
            => BluffEvaluator.Werte(new BluffRunde(optionen ?? Optionen(), wahl), Feld, konten ?? Kontostaende.OhneGrenze);

        [TestMethod]
        public void TheRealOneBringsTheFieldPointsAndEveryFoolHalf()
        {
            var e = Werte(new() { [Anna] = 1, [Bert] = 2, [Carl] = 2, [Dora] = 3 });

            Assert.AreEqual(Feld, e.PunkteVon(Anna) - 2 * BluffEvaluator.PunkteReinfall(Feld), "Die echte bringt nicht die Feldpunkte.");
            Assert.AreEqual(Feld + 2 * 150, e.PunkteVon(Anna), "Zwei Reinfaelle auf Annas Bluff bringen nicht je die Haelfte.");
            Assert.AreEqual(150, e.PunkteVon(Bert), "Doras Reinfall auf Berts Bluff zaehlt nicht.");
            Assert.IsFalse(e.Vorschlaege.ContainsKey(Carl), "Carl bekommt etwas, ohne zu finden und ohne Reinfall.");
            Assert.AreEqual("Bluff: echte gefunden, 2 hereingelegt +600", e.Vorschlaege[Anna].Grund);
            CollectionAssert.AreEquivalent(new[] { Anna }, e.Gewinner.ToArray());
        }

        [TestMethod]
        public void NobodyFindingTheRealOneStillCountsTheFools()
        {
            var e = Werte(new() { [Anna] = 3, [Bert] = 2 });

            Assert.IsFalse(e.Gefunden(Anna) || e.Gefunden(Bert));
            Assert.AreEqual(150, e.PunkteVon(Anna));
            Assert.AreEqual(150, e.PunkteVon(Bert));
            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, e.Gewinner.ToArray(), "Bei Gleichstand ist nicht jeder Beste vorn.");
            Assert.IsTrue(BluffTafel.Ende(e, Spieler).Where(z => z.Sieger).Select(z => z.Name).SequenceEqual(["Anna", "Bert"]));
        }

        [TestMethod]
        public void NobodyChoosingBringsNothing()
        {
            var e = Werte(new());

            Assert.AreEqual(0, e.Vorschlaege.Count);
            Assert.AreEqual(0, e.Gewinner.Count);
        }

        /// <summary>Ein gestrichener Bluff bringt seinem Urheber nichts - auch wenn eine Wahl darauf landete.</summary>
        [TestMethod]
        public void AStruckBluffCountsForNothing()
        {
            var e = Werte(new() { [Anna] = 0, [Bert] = 0 });

            Assert.IsFalse(e.Vorschlaege.ContainsKey(Carl), "Der gestrichene Bluff bringt Reinfaelle.");
            Assert.IsFalse(e.Gefunden(Anna), "Eine Wahl auf den gestrichenen gilt als echte.");
        }

        /// <summary>Zwei gleiche Bluffs sind eine Antwort - jeder Reinfall zaehlt fuer beide Urheber.</summary>
        [TestMethod]
        public void AMergedBluffCountsForEveryAuthor()
        {
            List<BluffOption> optionen = [new(1, "Rom", true, []), new(2, "Paris", false, [Anna, Bert])];

            var e = Werte(new() { [Carl] = 2, [Dora] = 2 }, optionen);

            Assert.AreEqual(2 * 150, e.PunkteVon(Anna));
            Assert.AreEqual(2 * 150, e.PunkteVon(Bert));
        }

        /// <summary>E2 kappt nur ein Minus: ein Plus bleibt, auch bei einem Konto unter null in einem Spiel ohne Minus.</summary>
        [TestMethod]
        public void APlusIsNeverCapped()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = -900 }, UnterNullErlaubt: false);

            Assert.AreEqual(Feld + 150, Werte(new() { [Anna] = 1, [Bert] = 2 }, konten: konten).PunkteVon(Anna));
        }

        [TestMethod]
        public void TheResolutionNamesAuthorsAndFoolsOnlyAfterTheChoice()
        {
            string Name(Guid id) => Spieler.Single(s => s.Item1 == id).Item2;

            var zeilen = BluffEvaluator.Aufloesung(Werte(new() { [Anna] = 1, [Bert] = 2, [Carl] = 2 }), Name);

            CollectionAssert.AreEqual(new[]
            {
                "✓ echt – Rom – gefunden: Anna",
                "Bluff von Anna – Paris – hereingefallen: Bert, Carl",
                "Bluff von Bert – Wien – hereingefallen: niemand",
                "gestrichen – rom (von Carl)",
            }, zeilen.ToArray());
        }

        [TestMethod]
        public void TheProfileNeedsThreePlayersAndPlaysTwoPhases()
        {
            var profil = MinigameProfiles.For(MinigameKind.Bluff);

            Assert.AreEqual(3, profil.MinPlayers);
            Assert.AreEqual(2, profil.FixedRounds);
            Assert.IsFalse(profil.ShowRounds);
            Assert.AreEqual(1, profil.MinOptions);
            Assert.IsFalse(profil.CountsAsOption("  "));

            var frage = new MinigameQuestion();
            profil.ApplyDefaults(frage);
            Assert.IsTrue(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions), "Ohne echte Antwort speicherbar.");
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 10, StepText = "Friedenspfeife" });
            Assert.IsFalse(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions));
        }
    }
}
