using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Der Erklaerbaer (M35 C6) als reine Funktion: Rater und Erklaerer bekommen je den Topf, eine verratene Runde
    /// bringt niemandem etwas, niemand richtig nichts, Gleichstand markiert beide; ein Plus wird nie gekappt. Dazu
    /// Profil und Pruefung: je Runde ein Begriff.
    /// </summary>
    [TestClass]
    public class ErklaerbaerUnitTests
    {
        private const int Feld = 300;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static readonly (Guid, string)[] Spieler = [(Anna, "Anna"), (Bert, "Bert"), (Carl, "Carl")];

        [TestMethod]
        [DataRow(300, 3, 100)]
        [DataRow(200, 3, 67)]
        [DataRow(100, 3, 33)]
        [DataRow(300, 0, 0)]
        public void TheShareIsTheFieldPointsPerRoundRounded(int feld, int runden, int topf)
            => Assert.AreEqual(topf, ErklaerbaerEvaluator.Topf(feld, runden));

        [TestMethod]
        public void AGuessedTermBringsTheRaterAndTheExplainerTheShare()
        {
            var e = ErklaerbaerEvaluator.Werte(
                [new(Anna, Bert), new(Anna, Carl), new(Anna, null)], 3, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(200, e.PunkteVon(Anna), "Der Erklaerer bekommt nicht je erratenem Begriff den Topf.");
            Assert.AreEqual(100, e.PunkteVon(Bert));
            Assert.AreEqual(100, e.PunkteVon(Carl));
            Assert.AreEqual("Erklärbär: 0× erraten, 2× erklärt +200", e.Vorschlaege[Anna].Grund);
            Assert.AreEqual("Erklärbär: 1× erraten, 0× erklärt +100", e.Vorschlaege[Bert].Grund);
            CollectionAssert.AreEquivalent(new[] { Anna }, e.Gewinner.ToArray());
        }

        [TestMethod]
        public void NobodyRightBringsNothing()
        {
            var e = ErklaerbaerEvaluator.Werte([new(Anna, null), new(Bert, null)], 2, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(0, e.Vorschlaege.Count, "Ohne Treffer gibt es einen Vorschlag.");
            Assert.AreEqual(0, e.Gewinner.Count);
            Assert.IsFalse(ErklaerbaerTafel.Stand(e, Spieler, true).Any(z => z.Sieger), "Ohne Punkte ist jemand Sieger.");
        }

        /// <summary>Verraten: weder der Erklaerer noch der Rater bekommen etwas fuer diese Runde - die anderen zaehlen.</summary>
        [TestMethod]
        public void ABetrayedRoundBringsNobodyAnything()
        {
            var e = ErklaerbaerEvaluator.Werte([new(Anna, Bert, Verraten: true), new(Anna, Carl)], 2, Feld, Kontostaende.OhneGrenze);

            Assert.IsFalse(e.Vorschlaege.ContainsKey(Bert), "Der Rater einer verratenen Runde bekommt etwas.");
            Assert.AreEqual(150, e.PunkteVon(Anna), "Der Erklaerer bekommt die verratene Runde.");
            Assert.AreEqual(150, e.PunkteVon(Carl));
            Assert.AreEqual(1, e.ErklaertVon(Anna));
        }

        [TestMethod]
        public void ATieMarksBothAtTheEnd()
        {
            var e = ErklaerbaerEvaluator.Werte([new(Anna, Bert), new(Bert, Anna)], 2, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(e.PunkteVon(Anna), e.PunkteVon(Bert));
            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, e.Gewinner.ToArray());

            var tafel = ErklaerbaerTafel.Stand(e, Spieler, true);
            Assert.IsTrue(tafel.Single(z => z.Name == "Anna").Sieger && tafel.Single(z => z.Name == "Bert").Sieger, "Bei Gleichstand ist nicht jeder Beste markiert.");
            Assert.IsFalse(ErklaerbaerTafel.Stand(e, Spieler, false).Any(z => z.Sieger), "Vor dem Ende ist schon jemand Sieger.");
        }

        /// <summary>E2 kappt nur ein Minus: ein Plus bleibt, auch bei einem Konto unter null in einem Spiel ohne Minus.</summary>
        [TestMethod]
        public void APlusIsNeverCapped()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = -500, [Bert] = -50 }, UnterNullErlaubt: false);

            var e = ErklaerbaerEvaluator.Werte([new(Anna, Bert)], 1, Feld, konten);

            Assert.AreEqual(Feld, e.PunkteVon(Anna));
            Assert.AreEqual(Feld, e.PunkteVon(Bert));
        }

        [TestMethod]
        public void TheOutcomeNamesTheTermOnlyWithTheRound()
        {
            string Name(Guid id) => Spieler.Single(s => s.Item1 == id).Item2;

            Assert.AreEqual("Bert hat „Käse“ erraten – erklärt von Anna", ErklaerbaerEvaluator.Ausgang(new(Anna, Bert), "Käse", Name));
            Assert.AreEqual("Niemand hat „Käse“ erraten", ErklaerbaerEvaluator.Ausgang(new(Anna, null), "Käse", Name));
            Assert.AreEqual("„Käse“ – verraten, zählt nicht", ErklaerbaerEvaluator.Ausgang(new(Anna, Bert, true), "Käse", Name));
        }

        [TestMethod]
        public void TheProfileNeedsTwoPlayersAndLetsTheMasterPickOnlyTheExplainer()
        {
            var profil = MinigameProfiles.For(MinigameKind.ExplainIt);

            Assert.AreEqual(2, profil.MinPlayers);
            Assert.IsTrue(profil.ChooserOnly && profil.ShowsPairChoice, "Der Spielleiter kann den Erklaerer nicht waehlen.");
            Assert.IsFalse(profil.ChoosesPartner, "Der Erklaerbaer fragt nach einem Gegenueber.");
            Assert.AreEqual("Wer erklärt", profil.ChooserLabel);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, profil.Layout);
            Assert.IsFalse(profil.CountsAsOption("?!"), "Ein Begriff ohne Buchstaben zaehlt.");
        }

        /// <summary>V2: je Runde ein eigener Begriff - mit weniger Begriffen als Runden meldet die Pruefung „zu wenige".</summary>
        [TestMethod]
        public void EveryRoundNeedsItsOwnTerm()
        {
            var frage = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.ExplainIt).ApplyDefaults(frage);
            frage.Rounds = 3;

            foreach (var (text, nummer) in new[] { ("Leuchtturm", 10), ("Zahnbürste", 20), ("?!", 30) })
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = nummer, StepText = text });

            var meldung = MinigameChecks.Content(frage).SingleOrDefault(m => m.Code == MinigameChecks.TooFewOptions);
            Assert.IsNotNull(meldung, "Zwei Begriffe fuer drei Runden gehen durch.");
            StringAssert.Contains(meldung.Message, "3 Begriffe");

            frage.Rounds = 2;
            Assert.IsFalse(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions), "Zwei Begriffe fuer zwei Runden sind zu wenige.");
        }
    }
}
