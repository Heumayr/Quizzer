using LocalBuzzer.Service.Base.States;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Collections.Concurrent;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Mehrheit auf der Spielerkarte (M35 B2): die Abgaben kommen ueber die Tastenwahl, der Vorschlag
    /// geht an die groesste Gruppe - mit den Feldpunkten, nie mit Minus (E1).
    /// </summary>
    [TestClass]
    public class MehrheitSpielerkarteUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            if (world != null)
                await world.DisposeAsync();

            UserPrompt.Reset();
        }

        /// <summary>Oeffnet eine Mehrheit mit drei Optionen und drei Spielern; liefert Ergebnisfenster und Tasten.</summary>
        private async Task<(PlayersResultViewModel Fenster, List<string> Tasten)> OeffneAsync(bool minderheit = false)
        {
            world = await TestGameBuilder.CreateAsync(QuestionType.Minigame, normalStepCount: 3, playerCount: 3);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            var fenster = vm.PlayersResultViewModel!;
            var frage = (MinigameQuestion)fenster.Coordinate!.QuestionBase!;

            if (minderheit)
                frage.Variant = MehrheitEvaluator.MinorityVariant;

            var tasten = frage.Steps
                .Where(s => !s.IsStart && !s.IsFinish && !string.IsNullOrEmpty(s.QuestionViewKey))
                .Select(s => s.QuestionViewKey)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            Assert.AreEqual(3, tasten.Count, "Die Optionen tragen keine Tasten - dann misst die Probe nichts.");

            return (fenster, tasten);
        }

        private static KeyValuePair<Guid, BuzzerKeySelector.SelectionResult> Wahl(PlayerResultContext karte, string taste)
            => new(karte.Player.Id, new BuzzerKeySelector.SelectionResult
            {
                PlayerId = karte.Player.Id,
                SelectedKeys = [taste],
                CommittedResult = true,
            });

        /// <summary>Die groesste Gruppe bekommt „richtig" mit den Feldpunkten; die andere nichts, kein Minus.</summary>
        [TestMethod]
        public async Task TheLargestGroupGetsTheFieldPoints()
        {
            var (fenster, tasten) = await OeffneAsync();
            var karten = fenster.PlayerResultContextList;

            fenster.SelectionResultsDic = new ConcurrentDictionary<Guid, BuzzerKeySelector.SelectionResult>(
                [Wahl(karten[0], tasten[0]), Wahl(karten[1], tasten[0]), Wahl(karten[2], tasten[1])]);

            Assert.AreEqual(ScoreSuggestion.Right, karten[0].Suggestion);
            Assert.AreEqual(ScoreSuggestion.Right, karten[1].Suggestion);
            Assert.AreEqual(ScoreSuggestion.None, karten[2].Suggestion, "Die kleinere Gruppe bekommt einen Vorschlag.");
            Assert.IsTrue(karten[0].CurrentScoreManipulation > 0);
            Assert.AreEqual(world.Coordinate.CurrentPoints, karten[0].CurrentScoreManipulation, "Nicht die Feldpunkte.");
            StringAssert.Contains(karten[2].ReihenfolgeText, "nicht in der größten Gruppe");
        }

        /// <summary>
        /// Der <b>Verlauf</b>, nicht nur der Endstand: liegt eine Gruppe erst vorn und wird dann
        /// ueberholt, verliert sie ihren Vorschlag wieder.
        /// </summary>
        [TestMethod]
        public async Task ALaterVoteMovesTheSuggestion()
        {
            var (fenster, tasten) = await OeffneAsync();
            var karten = fenster.PlayerResultContextList;

            fenster.SetSelectionResult(Wahl(karten[2], tasten[1]).Value);
            Assert.AreEqual(ScoreSuggestion.Right, karten[2].Suggestion, "Allein ist man die größte Gruppe.");

            fenster.SetSelectionResult(Wahl(karten[0], tasten[0]).Value);
            fenster.SetSelectionResult(Wahl(karten[1], tasten[0]).Value);

            Assert.AreEqual(ScoreSuggestion.None, karten[2].Suggestion, "Die überholte Gruppe behält ihren Vorschlag.");
            Assert.AreEqual(ScoreSuggestion.Right, karten[0].Suggestion);
        }

        /// <summary>Wer nicht abgibt, bekommt keinen Vorschlag - und zaehlt nicht als Gruppe.</summary>
        [TestMethod]
        public async Task WithoutAVoteThereIsNoSuggestion()
        {
            var (fenster, tasten) = await OeffneAsync();
            var karten = fenster.PlayerResultContextList;

            fenster.SelectionResultsDic = new ConcurrentDictionary<Guid, BuzzerKeySelector.SelectionResult>(
                [Wahl(karten[0], tasten[0]), Wahl(karten[1], tasten[0])]);

            Assert.AreEqual(ScoreSuggestion.None, karten[2].Suggestion, "Ohne Abgabe ein Vorschlag.");
            Assert.AreEqual(0, karten[2].KeyViews.Count);
            Assert.AreEqual(ScoreSuggestion.Right, karten[0].Suggestion);
        }

        /// <summary>In der Spielweise „Minderheit" gewinnt die kleinste Gruppe, die groesste geht leer aus.</summary>
        [TestMethod]
        public async Task InTheMinorityVariantTheSmallestGroupWins()
        {
            var (fenster, tasten) = await OeffneAsync(minderheit: true);
            var karten = fenster.PlayerResultContextList;

            fenster.SelectionResultsDic = new ConcurrentDictionary<Guid, BuzzerKeySelector.SelectionResult>(
                [Wahl(karten[0], tasten[0]), Wahl(karten[1], tasten[0]), Wahl(karten[2], tasten[1])]);

            Assert.AreEqual(ScoreSuggestion.Right, karten[2].Suggestion);
            Assert.AreEqual(ScoreSuggestion.None, karten[0].Suggestion);
            StringAssert.Contains(karten[2].ReihenfolgeText, "in der kleinsten Gruppe");
        }
    }
}
