using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Teilen oder Nehmen (M35 C2) als reine Funktion - alle vier Faelle, nur Plus - und was die drei
    /// Arten mit Spielerwahl im Profil mitbringen (Mindestspieler, Anteil, Regeltext).
    /// </summary>
    [TestClass]
    public class TeilenNehmenUnitTests
    {
        private const int Topf = 100;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();

        private static string Name(Guid id) => id == Anna ? "Anna" : "Bert";

        private static TeilenNehmenErgebnis Werte(bool? nimmtAnna, bool? nimmtBert, int topf = Topf)
            => TeilenNehmenEvaluator.Werte(new TeilenNehmenPaar(Anna, nimmtAnna, Bert, nimmtBert), topf, Name);

        [TestMethod]
        public void BothShareAndEachGetsHalf()
        {
            var ergebnis = Werte(false, false);

            Assert.AreEqual(50, ergebnis.Vorschlaege[Anna].Punkte);
            Assert.AreEqual(50, ergebnis.Vorschlaege[Bert].Punkte);
            StringAssert.Contains(ergebnis.Vorschlaege[Anna].Grund, "mit Bert geteilt");
            Assert.AreEqual(13, Werte(false, false, topf: 25).Vorschlaege[Bert].Punkte, "Die Hälfte rundet kaufmaennisch.");
        }

        [TestMethod]
        public void OneTakesAndGetsEverything()
        {
            var annaNimmt = Werte(true, false);
            Assert.AreEqual(Topf, annaNimmt.Vorschlaege[Anna].Punkte);
            Assert.IsFalse(annaNimmt.Vorschlaege.ContainsKey(Bert), "Wer geteilt hat, bekommt keinen Vorschlag - nur Plus.");
            StringAssert.Contains(annaNimmt.Ausgang, "Anna nimmt alles");

            var bertNimmt = Werte(false, true);
            Assert.AreEqual(Topf, bertNimmt.Vorschlaege[Bert].Punkte);
            Assert.AreEqual(1, bertNimmt.Vorschlaege.Count);
        }

        [TestMethod]
        public void BothTakeAndNobodyGetsAnything()
        {
            var ergebnis = Werte(true, true);

            Assert.AreEqual(0, ergebnis.Vorschlaege.Count);
            StringAssert.Contains(ergebnis.Ausgang, "niemand");
        }

        [TestMethod]
        public void AMissingChoiceProposesNothingAndSaysWho()
        {
            var ergebnis = Werte(false, null);

            Assert.AreEqual(0, ergebnis.Vorschlaege.Count);
            StringAssert.Contains(ergebnis.Ausgang, "Keine Abgabe von Bert");
        }

        [TestMethod]
        public void NoProposalOfTheThreeIsEverNegativeForShareOrTake()
        {
            foreach (var a in new bool?[] { true, false, null })
                foreach (var b in new bool?[] { true, false, null })
                    Assert.IsTrue(Werte(a, b).Vorschlaege.Values.All(v => v.Punkte > 0), $"Minus bei {a}/{b}.");
        }

        // --- Profile der drei Arten (V2, Maske, Beamer) ---

        [TestMethod]
        [DataRow(MinigameKind.Duel, 2, true)]
        [DataRow(MinigameKind.RobberAndGuard, 3, false)]
        [DataRow(MinigameKind.ShareOrTake, 2, true)]
        public void ThePlayerChoiceKindsNeedTheirPlayersAndKnowWhoChooses(MinigameKind art, int mindestens, bool waehltPartner)
        {
            var profil = MinigameProfiles.For(art);
            var frage = new MinigameQuestion();
            profil.ApplyDefaults(frage);

            Assert.AreEqual(mindestens, profil.MinPlayers);
            Assert.AreEqual(waehltPartner, profil.ChoosesPartner);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout, "Die Art läuft nicht im Telefonrahmen.");
            Assert.AreEqual(MinigameChecks.TooFewPlayers, MinigameChecks.ForPlayers(frage, mindestens - 1).Single().Code);
            Assert.AreEqual(0, MinigameChecks.ForPlayers(frage, mindestens).Count());
            Assert.AreEqual(0, MinigameChecks.Content(frage).Count(), "Die Vorgaben der Art sind nicht speicherbar.");
        }

        [TestMethod]
        public void AShareOutsideOneToHundredIsAnErrorWhereTheKindReadsIt()
        {
            var duell = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.Duel).ApplyDefaults(duell);

            duell.Share = 0;
            Assert.AreEqual(MinigameChecks.ShareOutOfRange, MinigameChecks.Content(duell).Single().Code);
            duell.Share = 101;
            Assert.AreEqual(MinigameChecks.ShareOutOfRange, MinigameChecks.Content(duell).Single().Code);

            var teilen = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.ShareOrTake).ApplyDefaults(teilen);
            teilen.Share = 0;
            Assert.AreEqual(0, MinigameChecks.Content(teilen).Count(), "Teilen liest keinen Anteil - und meldet ihn nicht.");
        }

        [TestMethod]
        public void TheRulesTextShowsRoundsAndShare()
        {
            var duell = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.Duel).ApplyDefaults(duell);
            duell.Rounds = 5;
            duell.Share = 40;

            var text = MinigameProfiles.RulesFor(duell);

            StringAssert.Contains(text, "Best of 5");
            StringAssert.Contains(text, "40 %");
            Assert.IsFalse(text.Contains('{'), "Ein Platzhalter steht noch im Regeltext: " + text);
        }
    }
}
