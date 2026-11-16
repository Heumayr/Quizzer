using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Minispiel Reaktion (M35 B3): Plausibilitaet, Wertung ueber die Runden und das Profil, das das
    /// Telefon-Layout je Art festlegt.
    /// </summary>
    [TestClass]
    public class ReaktionUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Cleo = Guid.NewGuid();

        private static ReactionAttempt Zeit(int ms) => ReaktionEvaluator.Classify(false, ms)!;

        private static ReactionAttempt Fruehstart() => ReaktionEvaluator.Classify(true, null)!;

        private static Dictionary<Guid, ReactionAttempt> Runde(params (Guid Spieler, ReactionAttempt Versuch)[] versuche)
            => versuche.ToDictionary(v => v.Spieler, v => v.Versuch);

        [TestMethod]
        public void TheBestSingleTimeOverAllRoundsWins()
        {
            var ergebnis = ReaktionEvaluator.Werte(
            [
                Runde((Anna, Zeit(300)), (Bert, Zeit(250))),
                Runde((Anna, Zeit(210)), (Bert, Zeit(400))),
            ]);

            Assert.IsTrue(ergebnis.IstGewinner(Anna), "Annas 210 ms in Runde 2 sind die beste Einzelzeit.");
            Assert.IsFalse(ergebnis.IstGewinner(Bert), "Bert war in keiner Runde der Schnellste ueberhaupt.");
            Assert.AreEqual(210, ergebnis.BestzeitVon(Anna));
            Assert.AreEqual(250, ergebnis.BestzeitVon(Bert));
        }

        [TestMethod]
        public void ATieLetsAllWin()
        {
            var ergebnis = ReaktionEvaluator.Werte([Runde((Anna, Zeit(222)), (Bert, Zeit(222)), (Cleo, Zeit(223)))]);

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, ergebnis.Gewinner.ToList());
        }

        [TestMethod]
        public void AFalseStartLosesTheRoundButNotTheGame()
        {
            // Berts 50 ms sind ein Fruehstart mit Zahl - er hat geraten, nicht reagiert.
            var ergebnis = ReaktionEvaluator.Werte(
            [
                Runde((Anna, Fruehstart()), (Bert, Zeit(300))),
                Runde((Anna, Zeit(280)), (Bert, Zeit(50))),
            ]);

            Assert.AreEqual(ReactionOutcome.FalseStart, Zeit(50).Outcome);
            Assert.AreEqual(280, ergebnis.BestzeitVon(Anna), "Der Fruehstart in Runde 1 darf nicht als Zeit zaehlen.");
            Assert.IsTrue(ergebnis.IstGewinner(Anna), "Ein Fruehstart mit Zahl hat gewonnen.");
            Assert.AreEqual(300, ergebnis.BestzeitVon(Bert));
        }

        [TestMethod]
        [DataRow(false, 99, ReactionOutcome.FalseStart)]
        [DataRow(false, 100, ReactionOutcome.Valid)]
        [DataRow(false, 5000, ReactionOutcome.Valid)]
        [DataRow(false, 5001, ReactionOutcome.TooSlow)]
        [DataRow(true, 300, ReactionOutcome.FalseStart)]
        public void TheServerChecksThePlausibility(bool fruehstart, int ms, ReactionOutcome erwartet)
        {
            Assert.AreEqual(erwartet, ReaktionEvaluator.Classify(fruehstart, ms)!.Outcome);
        }

        [TestMethod]
        public void AnUnusableSubmissionIsDiscarded()
        {
            Assert.IsNull(ReaktionEvaluator.Classify(false, null), "Weder Fruehstart noch Zeit - verwerfen.");
            Assert.IsNull(ReaktionEvaluator.Classify(false, -5), "Eine negative Zeit kommt nicht von einem echten Telefon.");
        }

        [TestMethod]
        public void TooSlowAndFalseStartsAloneGiveNoSuggestion()
        {
            var ergebnis = ReaktionEvaluator.Werte([Runde((Anna, Zeit(5400)), (Bert, Fruehstart()))]);

            Assert.AreEqual(0, ergebnis.Gewinner.Count, "Ohne gueltige Zeit gewinnt niemand.");
            Assert.IsNull(ergebnis.BestzeitVon(Anna));
            Assert.IsTrue(ergebnis.Abgegeben.Contains(Anna) && ergebnis.Abgegeben.Contains(Bert));
        }

        [TestMethod]
        public void NoSubmissionMeansNoTimeAndNoWin()
        {
            var ergebnis = ReaktionEvaluator.Werte([Runde((Anna, Zeit(300)))]);

            Assert.IsFalse(ergebnis.Abgegeben.Contains(Bert), "Ein getrenntes Telefon hat nichts abgegeben.");
            Assert.IsNull(ergebnis.BestzeitVon(Bert));
            Assert.IsFalse(ergebnis.IstGewinner(Bert));
        }

        [TestMethod]
        public void TheKindDecidesThePhoneLayout()
        {
            var typ = QuestionTypeProfiles.For(QuestionType.Minigame);
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);

            Assert.AreEqual(BuzzerControlsLayout.KeySelect, frage.BuzzerControlsLayout, "Die Mehrheit bleibt auf der Tastenwahl.");

            MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(frage);

            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout, "Die Reaktion laeuft im Minispiel-Rahmen.");
            Assert.AreEqual(3, frage.Rounds);
            Assert.IsTrue(typ.MatchesOwnedValues(frage), "Eine frische Reaktion weicht von ihren eigenen Vorgaben ab.");

            frage.BuzzerControlsLayout = BuzzerControlsLayout.KeySelect;
            Assert.IsFalse(typ.MatchesOwnedValues(frage), "Eine Reaktion auf der Tastenwahl faellt nicht auf.");

            typ.ApplyTo(frage);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout, "Die Reparatur stellt das Layout der Art nicht her.");
        }

        [TestMethod]
        [DataRow(0, true)]
        [DataRow(1, false)]
        [DataRow(MinigameProfile.MaxRounds, false)]
        [DataRow(MinigameProfile.MaxRounds + 1, true)]
        public void TheRoundsAreChecked(int runden, bool fehler)
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(frage);
            frage.Rounds = runden;

            var meldungen = MinigameChecks.Content(frage).ToList();

            Assert.AreEqual(fehler, meldungen.Any(m => m.Code == MinigameChecks.RoundsOutOfRange),
                string.Join(" | ", meldungen.Select(m => m.Message)));
            Assert.IsFalse(meldungen.Any(m => m.Code == MinigameChecks.TooFewOptions), "Die Reaktion braucht keine Zeilen.");
        }

        [TestMethod]
        public void AloneThereIsNoReaction()
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(frage);

            Assert.IsTrue(MinigameChecks.ForPlayers(frage, 1).Any(m => m.Code == MinigameChecks.TooFewPlayers));
            Assert.IsFalse(MinigameChecks.ForPlayers(frage, 2).Any());
        }
    }
}
