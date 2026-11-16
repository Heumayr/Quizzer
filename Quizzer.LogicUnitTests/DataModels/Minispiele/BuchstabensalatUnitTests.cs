using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Der Buchstabensalat (M35 C5) als reine Funktion: Vergleich ohne Gross/klein, Umlaute gleichgesetzt,
    /// Leerzeichen aussen egal; das Wirrwarr gleicht nie dem Begriff; die Wertung mit Gleichstand, null Treffern
    /// und ohne Kappung eines Plus.
    /// </summary>
    [TestClass]
    public class BuchstabensalatUnitTests
    {
        private const int Feld = 300;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        [TestMethod]
        [DataRow("Käse", "Käse")]
        [DataRow("käse", "Käse")]
        [DataRow("KAESE", "Käse")]
        [DataRow("  Kaese  ", "Käse")]
        [DataRow("Kaese", "Käse")]
        [DataRow("Käse", "Kaese")]
        [DataRow("Eichhoernchen", "Eichhörnchen")]
        [DataRow("muell", "Müll")]
        [DataRow("FUSSBALL", "Fußball")]
        [DataRow("fußball", "FUSSBALL")]
        [DataRow("new  york", "New York")]
        [DataRow("\tNew York\n", "New York")]
        public void TheseMatch(string eingabe, string begriff)
            => Assert.IsTrue(BuchstabensalatBegriffe.Gleich(eingabe, begriff), $"„{eingabe}“ trifft „{begriff}“ nicht.");

        [TestMethod]
        [DataRow("Kase", "Käse")]
        [DataRow("Kaes", "Käse")]
        [DataRow("Käsee", "Käse")]
        [DataRow("NewYork", "New York")]
        [DataRow("", "Käse")]
        [DataRow("   ", "Käse")]
        [DataRow("SÄKE", "Käse")]
        public void TheseDoNotMatch(string eingabe, string begriff)
            => Assert.IsFalse(BuchstabensalatBegriffe.Gleich(eingabe, begriff), $"„{eingabe}“ gilt als „{begriff}“.");

        /// <summary>
        /// Das Wirrwarr hat genau die Buchstaben des Begriffs und gleicht ihm nie - ueber viele Startwerte, auch fuer
        /// Begriffe, die beim Mischen leicht wieder sich selbst ergeben, und in der Vergleichsform (Otto, Anna).
        /// </summary>
        [TestMethod]
        [DataRow("Käse")]
        [DataRow("Otto")]
        [DataRow("Anna")]
        [DataRow("ab")]
        [DataRow("New York")]
        [DataRow("Schmetterling")]
        public void TheScrambleNeverEqualsTheTermAndKeepsItsLetters(string begriff)
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var wirrwarr = BuchstabensalatBegriffe.Verwuerfle(begriff, new Random(seed).Next);

                Assert.IsNotNull(wirrwarr, $"{begriff} liess sich nicht verwuerfeln.");
                Assert.IsFalse(BuchstabensalatBegriffe.Gleich(wirrwarr, begriff), $"Startwert {seed}: das Wirrwarr „{wirrwarr}“ ist die Loesung.");
                Assert.AreEqual(wirrwarr.ToUpperInvariant(), wirrwarr, "Das Wirrwarr ist nicht gross geschrieben.");
                CollectionAssert.AreEquivalent(begriff.ToUpperInvariant().ToList(), wirrwarr.ToList(), "Die Buchstaben stimmen nicht.");
                Assert.AreEqual(begriff.IndexOf(' '), wirrwarr.IndexOf(' '), "Das Leerzeichen ist gewandert.");
            }
        }

        [TestMethod]
        [DataRow("a")]
        [DataRow("aa")]
        [DataRow("Aaa")]
        [DataRow("1 2")]
        [DataRow("")]
        public void ATermWithoutTwoDifferentLettersIsNotPlayable(string begriff)
        {
            Assert.IsNull(BuchstabensalatBegriffe.Verwuerfle(begriff, new Random(1).Next));
            Assert.IsFalse(BuchstabensalatBegriffe.Verwuerfelbar(begriff));
        }

        [TestMethod]
        [DataRow(3, 3, 300)]
        [DataRow(1, 3, 100)]
        [DataRow(2, 3, 200)]
        [DataRow(1, 4, 75)]
        [DataRow(0, 3, 0)]
        [DataRow(1, 0, 0)]
        public void ThePointsAreTheShareOfTheField(int geloest, int begriffe, int erwartet)
            => Assert.AreEqual(erwartet, BuchstabensalatEvaluator.Punkte(geloest, begriffe, Feld));

        [TestMethod]
        public void ATieGivesEqualPointsAndBothAreMarkedAsTheMost()
        {
            var e = BuchstabensalatEvaluator.Werte(new Dictionary<Guid, BuchstabensalatStand>
            {
                [Anna] = new(2),
                [Bert] = new(2),
                [Carl] = new(0),
            }, 5, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(120, e.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(e.Vorschlaege[Anna].Punkte, e.Vorschlaege[Bert].Punkte);
            Assert.IsFalse(e.Vorschlaege.ContainsKey(Carl), "Null geloeste bringen einen Vorschlag.");
            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, e.Gewinner.ToList());
            StringAssert.Contains(e.Vorschlaege[Anna].Grund, "2 gelöst von 5");

            var zeilen = BuchstabensalatTafel.Ende(e, [(Carl, "Carl"), (Bert, "Bert"), (Anna, "Anna")]);
            Assert.IsTrue(zeilen[0].Sieger && zeilen[1].Sieger && !zeilen[2].Sieger, "Am Beamer sind nicht beide markiert.");
            Assert.AreEqual("2 gelöst · +120", zeilen[0].Zeit);
        }

        [TestMethod]
        public void NoHitsBringNoProposalAndNoWinner()
        {
            var e = BuchstabensalatEvaluator.Werte(new Dictionary<Guid, BuchstabensalatStand> { [Anna] = new(0) }, 4, Feld, Kontostaende.OhneGrenze);

            Assert.AreEqual(0, e.Vorschlaege.Count);
            Assert.AreEqual(0, e.Gewinner.Count);
            Assert.AreEqual("Kein Versuch – von Hand werten", BuchstabensalatEvaluator.Describe(e, Bert));
        }

        /// <summary>E2 kappt nur ein Minus - ein Spieler tief im Minus bekommt sein Plus ungekappt.</summary>
        [TestMethod]
        public void APlusIsNeverCapped()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Anna] = -500 }, UnterNullErlaubt: false);

            var e = BuchstabensalatEvaluator.Werte(new Dictionary<Guid, BuchstabensalatStand> { [Anna] = new(3) }, 3, Feld, konten);

            Assert.AreEqual(Feld, e.Vorschlaege[Anna].Punkte);
        }

        /// <summary>V2: mit zwei spielbaren Begriffen meldet die Pruefung „zu wenige Begriffe" - ein unverwuerfelbarer zaehlt nicht mit.</summary>
        [TestMethod]
        public void AWordScrambleNeedsThreePlayableTerms()
        {
            var frage = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.WordScramble).ApplyDefaults(frage);

            foreach (var (text, nummer) in new[] { ("Käse", 10), ("Hund", 20), ("aa", 30) })
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = nummer, StepText = text });

            var meldung = MinigameChecks.Content(frage).SingleOrDefault(m => m.Code == MinigameChecks.TooFewOptions);
            Assert.IsNotNull(meldung, "Ein unverwuerfelbarer Begriff zaehlt als dritter.");
            StringAssert.Contains(meldung.Message, "3 Begriffe");

            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 40, StepText = "Lampe" });
            Assert.IsFalse(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions), "Drei spielbare Begriffe sind zu wenige.");
        }

        [TestMethod]
        public void TheResolutionListsTheUnsolvedTerms()
            => CollectionAssert.AreEqual(new[] { "ungelöst – Käse", "ungelöst – Fußball" },
                BuchstabensalatTafel.Aufloesung(["Käse", "Fußball"]).ToArray());
    }
}
