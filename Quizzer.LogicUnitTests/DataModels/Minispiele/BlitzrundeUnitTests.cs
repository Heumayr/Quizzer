using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Die Blitzrunde (M35 C5) als reine Funktion: richtig +1, falsch −1, daraus der Anteil der Feldpunkte -
    /// Gleichstand, null Treffer, ein Minus gekappt (E2), ein Plus nie. Dazu der Inhalt aus den Zeilen.
    /// </summary>
    [TestClass]
    public class BlitzrundeUnitTests
    {
        private const int Feld = 200;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        [TestMethod]
        [DataRow(8, 0, 8, 200)]
        [DataRow(0, 8, 8, -200)]
        [DataRow(5, 2, 8, 75)]
        [DataRow(2, 5, 8, -75)]
        [DataRow(4, 4, 8, 0)]
        [DataRow(1, 0, 3, 67)]
        [DataRow(0, 1, 3, -67)]
        [DataRow(1, 0, 0, 0)]
        public void ThePointsAreTheNetShareOfTheField(int richtig, int falsch, int aussagen, int erwartet)
            => Assert.AreEqual(erwartet, BlitzrundeEvaluator.Punkte(new(richtig, falsch), aussagen, Feld));

        /// <summary>Gleichstand heisst hier gleicher Anteil - nicht ein Sieger, der alles bekommt.</summary>
        [TestMethod]
        public void EqualNetScoresGetEqualPointsWhateverTheyAnswered()
        {
            var e = BlitzrundeEvaluator.Werte(new Dictionary<Guid, BlitzrundeStand>
            {
                [Anna] = new(5, 1),
                [Bert] = new(4, 0),
                [Carl] = new(2, 2),
            }, 8, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(100, e.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(e.Vorschlaege[Anna].Punkte, e.Vorschlaege[Bert].Punkte, "Gleiches Netto, verschiedene Punkte.");
            Assert.IsFalse(e.Vorschlaege.ContainsKey(Carl), "Null Netto bringt einen Vorschlag.");
            StringAssert.Contains(e.Vorschlaege[Anna].Grund, "5 richtig, 1 falsch");
        }

        [TestMethod]
        public void NoHitsAndNoAnswersBringNoProposal()
        {
            var e = BlitzrundeEvaluator.Werte(new Dictionary<Guid, BlitzrundeStand>(), 8, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(0, e.Vorschlaege.Count);
            Assert.AreEqual("Keine Antwort – von Hand werten", BlitzrundeEvaluator.Describe(e, Anna));
        }

        /// <summary>E2: das Minus kappt am Kontostand, das Plus nie - auch nicht bei einem Spieler im Minus.</summary>
        [TestMethod]
        public void OnlyTheMinusIsCappedAtTheBalance()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = 50, [Bert] = -300 }, UnterNullErlaubt: false);

            var e = BlitzrundeEvaluator.Werte(new Dictionary<Guid, BlitzrundeStand>
            {
                [Anna] = new(0, 8),
                [Bert] = new(8, 0),
            }, 8, Feld, konten);

            Assert.AreEqual(-50, e.Vorschlaege[Anna].Punkte, "Das Minus ist nicht am Kontostand gekappt.");
            StringAssert.Contains(e.Vorschlaege[Anna].Grund, Punkteanteil.GekapptText);
            Assert.AreEqual(Feld, e.Vorschlaege[Bert].Punkte, "Ein Plus wurde gekappt.");
            Assert.IsFalse(e.Vorschlaege[Bert].Grund.Contains(Punkteanteil.GekapptText));

            var frei = BlitzrundeEvaluator.Werte(new Dictionary<Guid, BlitzrundeStand> { [Anna] = new(0, 8) }, 8, Feld,
                konten with { UnterNullErlaubt = true });
            Assert.AreEqual(-Feld, frei.Vorschlaege[Anna].Punkte, "Erlaubt das Spiel ein Minus, wird trotzdem gekappt.");
        }

        /// <summary>Waehrend der Runde zeigt der Beamer nur den Fortschritt - kein Wort, das richtig oder falsch verriete.</summary>
        [TestMethod]
        public void TheRunningBoardShowsProgressOnly()
        {
            var zeilen = BlitzrundeTafel.Rennen(new Dictionary<Guid, int> { [Anna] = 3 }, 8, [(Anna, "Anna"), (Bert, "Bert")]);

            Assert.AreEqual("3 von 8 beantwortet", zeilen[0].Zeit);
            Assert.AreEqual("0 von 8 beantwortet", zeilen[1].Zeit);
            Assert.IsFalse(zeilen.Any(z => z.Sieger || z.Zeit.Contains("richtig") || z.Zeit.Contains("falsch")));
        }

        [TestMethod]
        public void TheFinalBoardMarksTheBestPositiveScore()
        {
            var e = BlitzrundeEvaluator.Werte(new Dictionary<Guid, BlitzrundeStand> { [Anna] = new(6, 0), [Bert] = new(1, 3) }, 8, Feld,
                Kontostaende.OhneGrenze);

            var zeilen = BlitzrundeTafel.Ende(e, [(Bert, "Bert"), (Anna, "Anna"), (Carl, "Carl")]);

            Assert.AreEqual("Anna", zeilen[0].Name);
            Assert.IsTrue(zeilen[0].Sieger);
            Assert.AreEqual("6 richtig, 0 falsch · +150", zeilen[0].Zeit);
            Assert.AreEqual("keine Antwort", zeilen.Single(z => z.Name == "Carl").Zeit);
            Assert.IsFalse(zeilen.Single(z => z.Name == "Bert").Sieger);
        }

        /// <summary>V2: mit drei Aussagen meldet die Pruefung „zu wenige Aussagen", mit vier nicht.</summary>
        [TestMethod]
        public void ALightningRoundNeedsFourStatements()
        {
            var frage = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.LightningRound).ApplyDefaults(frage);

            for (var i = 1; i <= 3; i++)
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = i * 10, StepText = $"Aussage {i}" });

            var meldung = MinigameChecks.Content(frage).SingleOrDefault(m => m.Code == MinigameChecks.TooFewOptions);
            Assert.IsNotNull(meldung, "Drei Aussagen fallen der Pruefung nicht auf.");
            StringAssert.Contains(meldung.Message, "4 Aussagen");

            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 40, StepText = "Aussage 4" });
            Assert.IsFalse(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions), "Vier Aussagen sind zu wenige.");
        }

        /// <summary>V13: eine Zeile aus Leerzeichen zaehlt in der Pruefung so wenig wie im Spiel - drei echte und eine leere sind zu wenige.</summary>
        [TestMethod]
        public void ABlankRowCountsNeitherInTheCheckNorInTheGame()
        {
            var frage = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.LightningRound).ApplyDefaults(frage);

            for (var i = 1; i <= 3; i++)
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = i * 10, StepText = $"Aussage {i}" });

            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 40, StepText = "   ", Designation = " " });

            Assert.AreEqual(3, MinispielInhalt.Zeilen(frage).Count, "Das Spiel spielt die leere Zeile - dann misst die Probe das Falsche.");
            Assert.IsTrue(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions),
                "Die Pruefung zaehlt die leere Zeile als vierte Aussage.");
        }

        /// <summary>Der Inhalt: gewoehnliche Zeilen in ihrer Reihenfolge, mit Haken; Start, Abschluss und leere zaehlen nicht.</summary>
        [TestMethod]
        public void TheContentIsTheOrdinaryRowsWithTheirTicks()
        {
            var frage = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.LightningRound).ApplyDefaults(frage);
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 30, StepText = " Zweite ", IsResult = false });
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 10, StepText = "Erste", IsResult = true });
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 5, StepText = "Start", IsStart = true });
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 90, StepText = "Ende", IsFinish = true });
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 40, StepText = "  " });
            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 20, StepText = "", Designation = "Kurz", IsResult = true });

            CollectionAssert.AreEqual(
                new[] { new MinispielZeile("Erste", true), new MinispielZeile("Kurz", true), new MinispielZeile("Zweite", false) },
                MinispielInhalt.Zeilen(frage).ToArray());
        }

        /// <summary>Die Aufloesung nach der Runde: jede Aussage mit „wahr" oder „falsch" als Wort, nie nur als Farbe.</summary>
        [TestMethod]
        public void TheResolutionNamesTheTruthOfEveryStatementInWords()
        {
            CollectionAssert.AreEqual(
                new[] { "✓ wahr – Paris ist die Hauptstadt", "✗ falsch – Der Mond ist ein Planet" },
                BlitzrundeTafel.Aufloesung([new("Paris ist die Hauptstadt", true), new("Der Mond ist ein Planet", false)]).ToArray());
        }
    }
}
