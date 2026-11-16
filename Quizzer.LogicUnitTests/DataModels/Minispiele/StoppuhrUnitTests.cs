using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Minispiel Stoppuhr (M35 C1): Grenzen, Abstandswertung (geteilt mit der Schaetzfrage), Tafel und
    /// Profil mit der Zielzeit in <c>DurationSeconds</c>.
    /// </summary>
    [TestClass]
    public class StoppuhrUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Cleo = Guid.NewGuid();

        private static readonly (Guid Id, string Name)[] Spieler = [(Anna, "Anna"), (Bert, "Bert"), (Cleo, "Cleo")];

        private static StoppuhrVersuch Stopp(int ms, int ziel = 10) => StoppuhrEvaluator.Classify(ms, ziel)!;

        private static StoppuhrErgebnis Werte(params (Guid Spieler, int Ms)[] abgaben)
            => StoppuhrEvaluator.Werte(abgaben.ToDictionary(a => a.Spieler, a => Stopp(a.Ms)), zielSekunden: 10);

        [TestMethod]
        public void TheClosestTimeWins()
        {
            var ergebnis = Werte((Anna, 10230), (Bert, 9800));

            CollectionAssert.AreEquivalent(new[] { Bert }, ergebnis.Rangfolge.WinnerIds.ToList());
            Assert.AreEqual(230, ergebnis.AbstandVon(Anna));
            Assert.AreEqual(200, ergebnis.AbstandVon(Bert));
        }

        [TestMethod]
        public void ATieOnBothSidesLetsAllWin()
        {
            var ergebnis = Werte((Anna, 10200), (Bert, 9800), (Cleo, 11000));

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, ergebnis.Rangfolge.WinnerIds.ToList(),
                "Zu frueh und zu spaet zaehlen nicht gleich weit daneben.");
        }

        [TestMethod]
        public void TooLateStaysOutOfTheScoring()
        {
            // Ziel 10 s: ueber 20 s keine Wertung - auch wenn alle anderen weiter weg waeren.
            var ergebnis = Werte((Anna, 20001), (Bert, 19000));

            CollectionAssert.AreEquivalent(new[] { Bert }, ergebnis.Rangfolge.WinnerIds.ToList());
            Assert.IsNull(ergebnis.AbstandVon(Anna), "Eine Zeit ueber der Grenze hat einen Abstand.");
            Assert.IsFalse(ergebnis.VersuchVon(Anna)!.Gewertet);
            Assert.AreEqual(0, Werte((Anna, 20001)).Rangfolge.WinnerIds.Count, "Allein zu spaet hat gewonnen.");
        }

        [TestMethod]
        [DataRow(null, null)]
        [DataRow(-1, null)]
        [DataRow(0, true)]
        [DataRow(20000, true)]
        [DataRow(20001, false)]
        public void TheServerChecksThePlausibility(int? ms, bool? gewertet)
        {
            var versuch = StoppuhrEvaluator.Classify(ms, zielSekunden: 10);

            Assert.AreEqual(gewertet, versuch?.Gewertet, $"{ms} ms bei Ziel 10 s");
        }

        [TestMethod]
        public void NoSubmissionMeansNoTimeAndNoWin()
        {
            var ergebnis = Werte((Anna, 10000));

            Assert.IsNull(ergebnis.VersuchVon(Bert));
            Assert.IsNull(ergebnis.AbstandVon(Bert));
            Assert.IsFalse(ergebnis.IstGewinner(Bert));
            Assert.IsTrue(ergebnis.IstGewinner(Anna));
            Assert.AreEqual(0, StoppuhrEvaluator.Werte(new Dictionary<Guid, StoppuhrVersuch>(), 10).Rangfolge.WinnerIds.Count);
        }

        /// <summary>Dieselbe Rechnung wie die Schaetzfrage: gleiche Werte, gleiche Sieger, gleiche Reihenfolge.</summary>
        [TestMethod]
        public void TheEstimateAndTheStopwatchShareTheDistanceRanking()
        {
            var frage = new AppreciateQestion { ValueKind = AppreciateValueKind.Number, Unit = AppreciateUnit.Stueck, ExpectedValue = 10 };
            var schaetzung = AppreciateEvaluator.Evaluate(frage,
                new Dictionary<Guid, string?> { [Anna] = "10,5", [Bert] = "9,5", [Cleo] = "xyz" });

            var stoppuhr = StoppuhrEvaluator.Werte(new Dictionary<Guid, StoppuhrVersuch>
            {
                [Anna] = Stopp(10500), [Bert] = Stopp(9500), [Cleo] = Stopp(25000),
            }, 10);

            CollectionAssert.AreEquivalent(schaetzung.WinnerIds.ToList(), stoppuhr.Rangfolge.WinnerIds.ToList());
            CollectionAssert.AreEqual(
                schaetzung.Guesses.Select(g => g.PlayerId).ToList(), stoppuhr.Rangfolge.Guesses.Select(g => g.PlayerId).ToList(),
                "Ohne Wert steht nicht zuletzt.");
        }

        [TestMethod]
        public void TheTableShowsTimeAndDistanceTheClosestFirst()
        {
            var ergebnis = StoppuhrEvaluator.Werte(new Dictionary<Guid, StoppuhrVersuch>
            {
                [Anna] = Stopp(30000), [Bert] = Stopp(10000), [Cleo] = Stopp(10230),
            }, 10);

            var zeilen = StoppuhrTafel.Ende(ergebnis, Spieler.Append((Guid.NewGuid(), "Dora")));

            CollectionAssert.AreEqual(new[] { "Bert", "Cleo", "Anna", "Dora" }, zeilen.Select(z => z.Name).ToArray());
            Assert.AreEqual("10,00 s (genau getroffen)", zeilen[0].Zeit);
            Assert.AreEqual("★ Sieger", zeilen[0].SiegerText);
            Assert.AreEqual("10,23 s (0,23 s daneben)", zeilen[1].Zeit);
            Assert.AreEqual("zu spät – keine Wertung", zeilen[2].Zeit);
            Assert.AreEqual("keine Abgabe", zeilen[3].Zeit);
            Assert.IsFalse(zeilen.Any(z => z.HatBalken), "Die Stoppuhr zeigt Balken.");
        }

        [TestMethod]
        public void TheTargetLivesInTheDurationAndIsNamedSo()
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.Stopwatch).ApplyDefaults(frage);
            var art = MinigameProfiles.For(MinigameKind.Stopwatch);

            Assert.AreEqual(4, Convert.ToInt32(Enum.Parse<MinigameKind>("Stopwatch")), "Die Zahl steht in der Datenbank.");
            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout);
            Assert.AreEqual("Zielzeit (Sekunden)", art.DurationLabel, "Die Maske nennt die Zielzeit nicht so.");
            Assert.AreEqual(10, frage.DurationSeconds);

            frage.DurationSeconds = 12;
            StringAssert.Contains(MinigameProfiles.RulesFor(frage), "12 Sekunden", "Der Regeltext nennt die Zielzeit nicht.");
            Assert.IsFalse(MinigameChecks.Content(frage).Any());
            Assert.IsTrue(MinigameChecks.ForPlayers(frage, 1).Any(m => m.Code == MinigameChecks.TooFewPlayers));
        }

        /// <summary>Unter 5 s waere die Zielzeit noch zu sehen (3 s) - kein Spiel.</summary>
        [TestMethod]
        [DataRow(4, true)]
        [DataRow(5, false)]
        [DataRow(60, false)]
        [DataRow(61, true)]
        public void TheTargetIsChecked(int sekunden, bool fehler)
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.Stopwatch).ApplyDefaults(frage);
            frage.DurationSeconds = sekunden;

            Assert.AreEqual(fehler, MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.DurationOutOfRange));
        }
    }
}
