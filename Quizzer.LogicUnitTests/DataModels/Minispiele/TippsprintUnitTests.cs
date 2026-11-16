using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Minispiel Tippsprint (M35 C1): Plausibilitaet der Staende, Wertung, Rennen am Beamer und Profil.
    /// </summary>
    [TestClass]
    public class TippsprintUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Cleo = Guid.NewGuid();

        private static readonly (Guid Id, string Name)[] Spieler = [(Anna, "Anna"), (Bert, "Bert"), (Cleo, "Cleo")];

        private static MinigameQuestion Sprint()
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.TapSprint).ApplyDefaults(frage);
            return frage;
        }

        [TestMethod]
        public void MostTapsWin()
        {
            var ergebnis = TippsprintEvaluator.Werte(new Dictionary<Guid, int> { [Anna] = 41, [Bert] = 40 });

            CollectionAssert.AreEquivalent(new[] { Anna }, ergebnis.Gewinner.ToList());
            Assert.AreEqual(40, ergebnis.TippsVon(Bert));
        }

        [TestMethod]
        public void ATieLetsAllWin()
        {
            var ergebnis = TippsprintEvaluator.Werte(new Dictionary<Guid, int> { [Anna] = 40, [Bert] = 40, [Cleo] = 12 });

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, ergebnis.Gewinner.ToList());
        }

        [TestMethod]
        public void NoTapsAndNoSubmissionNeverWin()
        {
            var ergebnis = TippsprintEvaluator.Werte(new Dictionary<Guid, int> { [Anna] = 0, [Bert] = 0 });

            Assert.AreEqual(0, ergebnis.Gewinner.Count, "Null Tipps haben gewonnen.");
            Assert.AreEqual(0, TippsprintEvaluator.Werte(new Dictionary<Guid, int>()).Gewinner.Count);
            Assert.IsNull(ergebnis.TippsVon(Cleo), "Ein getrenntes Telefon hat einen Stand.");
            Assert.IsFalse(ergebnis.IstGewinner(Cleo));
        }

        /// <summary>Hoechstens 20 Tipps je Sekunde - genau an der Grenze geht es noch.</summary>
        [TestMethod]
        [DataRow(10, 500, true)]
        [DataRow(11, 500, false)]
        [DataRow(0, 0, true)]
        [DataRow(1, 0, false)]
        [DataRow(200, 10000, true)]
        [DataRow(201, 10000, false)]
        [DataRow(-1, 1000, false)]
        [DataRow(1, -1, false)]
        public void AtMostTwentyTapsPerSecond(int tipps, int ms, bool angenommen)
        {
            var stand = TippsprintEvaluator.Pruefe(null, new(tipps, ms, false), dauerSekunden: 10);

            Assert.AreEqual(angenommen, stand != null, $"{tipps} Tipps in {ms} ms");
        }

        [TestMethod]
        public void AStandNeverSinksAndTheFinalIsNotBelowTheLast()
        {
            var bisher = new TippsprintStand(10, 1000, false);

            Assert.IsNull(TippsprintEvaluator.Pruefe(bisher, new(9, 1500, false), 10), "Ein Zwischenstand ist gesunken.");
            Assert.IsNotNull(TippsprintEvaluator.Pruefe(bisher, new(10, 1500, false), 10), "Ein gleicher Stand wurde verworfen.");
            Assert.IsNull(TippsprintEvaluator.Pruefe(bisher, new(9, 10000, true), 10), "Der Endstand liegt unter dem letzten Zwischenstand.");
            Assert.IsNotNull(TippsprintEvaluator.Pruefe(bisher, new(10, 10000, true), 10));
        }

        [TestMethod]
        public void NothingCountsAfterTheFinalOrBeyondTheDuration()
        {
            Assert.IsNull(TippsprintEvaluator.Pruefe(new(50, 10000, true), new(51, 10000, true), 10), "Nach dem Endstand kam noch einer an.");
            Assert.IsNull(TippsprintEvaluator.Pruefe(null, new(5, 10001, false), 10), "Laenger als die Dauer gemessen und angenommen.");
            Assert.IsNotNull(TippsprintEvaluator.Pruefe(null, new(5, 10000, true), 10));
        }

        [TestMethod]
        public void TheRaceShowsNumberAndBarTheLeaderFull()
        {
            var staende = new Dictionary<Guid, TippsprintStand> { [Anna] = new(10, 1000, false), [Bert] = new(20, 1000, false) };

            var zeilen = TippsprintTafel.Rennen(staende, Spieler);

            CollectionAssert.AreEqual(new[] { "Bert", "Anna", "Cleo" }, zeilen.Select(z => z.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "20 Tipps", "10 Tipps", "0 Tipps" }, zeilen.Select(z => z.Zeit).ToArray(),
                "Die Zahl fehlt neben dem Balken.");
            CollectionAssert.AreEqual(new double?[] { 1.0, 0.5, 0.0 }, zeilen.Select(z => z.Balken).ToArray());
            Assert.IsTrue(zeilen.All(z => z.HatBalken && !z.Sieger), "Im laufenden Rennen gibt es schon Sieger.");
        }

        [TestMethod]
        public void TheEndMarksTheWinnerWithSymbolAndText()
        {
            var ergebnis = TippsprintEvaluator.Werte(new Dictionary<Guid, int> { [Anna] = 33, [Bert] = 41 });

            var zeilen = TippsprintTafel.Ende(ergebnis, Spieler);

            Assert.AreEqual("Bert", zeilen[0].Name);
            Assert.AreEqual("★ Sieger", zeilen[0].SiegerText);
            Assert.AreEqual(string.Empty, zeilen[1].SiegerText);
            Assert.AreEqual("keine Abgabe", zeilen.Single(z => z.Name == "Cleo").Zeit);
            Assert.AreEqual("1 Tipp", TippsprintEvaluator.Describe(1));
        }

        [TestMethod]
        public void TheProfilePlaysOnThePhoneForTheDuration()
        {
            var frage = Sprint();
            var typ = QuestionTypeProfiles.For(QuestionType.Minigame);

            Assert.AreEqual(3, Convert.ToInt32(Enum.Parse<MinigameKind>("TapSprint")), "Die Zahl steht in der Datenbank.");
            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout, "Der Tippsprint laeuft nicht im Minispiel-Rahmen.");
            Assert.IsTrue(typ.MatchesOwnedValues(frage), "Ein frischer Tippsprint weicht von seinen Vorgaben ab.");
            Assert.AreEqual(10, frage.DurationSeconds);
            Assert.AreEqual(1, frage.Rounds);

            frage.DurationSeconds = 15;
            StringAssert.Contains(MinigameProfiles.RulesFor(frage), "15 Sekunden", "Der Regeltext nennt die Dauer nicht.");
            Assert.IsFalse(MinigameProfiles.RulesFor(frage).Contains(MinigameProfiles.DurationPlaceholder));
            Assert.IsFalse(MinigameChecks.Content(frage).Any(), "Ein Tippsprint ohne Zeilen ist nicht speicherbar.");
        }

        [TestMethod]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(60, false)]
        [DataRow(61, true)]
        public void TheDurationIsChecked(int sekunden, bool fehler)
        {
            var frage = Sprint();
            frage.DurationSeconds = sekunden;

            var meldungen = MinigameChecks.Content(frage).ToList();

            Assert.AreEqual(fehler, meldungen.Any(m => m.Code == MinigameChecks.DurationOutOfRange && m.IsError),
                string.Join(" | ", meldungen.Select(m => m.Message)));
        }

        [TestMethod]
        public void AloneThereIsNoSprint()
        {
            var frage = Sprint();

            Assert.IsTrue(MinigameChecks.ForPlayers(frage, 1).Any(m => m.Code == MinigameChecks.TooFewPlayers));
            Assert.IsFalse(MinigameChecks.ForPlayers(frage, 2).Any());
        }
    }
}
