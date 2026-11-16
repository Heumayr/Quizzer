using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Sonderregeln;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Der Schritt „alle Karten gemeinsam werten" (M35 C3b): eine Regel bekommt die Bewertungen aller Karten
    /// der Zelle, und eine Karte, die niemand anfasst, wechselt mit der Bewertung einer anderen. Ohne Server -
    /// die Regel ist eine Testregel, die nur an Annas Bewertung haengt.
    /// </summary>
    [TestClass]
    public class SonderregelAlleKartenUnitTests
    {
        private const int Bonus = 10;
        private const int Strafe = -3;

        /// <summary>Bert folgt Anna: ist sie richtig, bekommt er seinen Wert +10; Carla und Anna selbst zahlen dann 3.</summary>
        private sealed class FolgtAnnaRegel(Guid anna, Guid bert, Guid carla)
            : SonderregelAblauf(new SpecialRuleProfile(CellSpecialRule.Stake, "Test", "Test"))
        {
            protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
            {
            }

            protected override string AbgabeText(Guid spieler) => string.Empty;

            protected override IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            {
                var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

                if (eingaenge[anna].Bewertung != RatingKind.Right)
                    return vorschlaege;

                vorschlaege[bert] = new(eingaenge[bert].Grundwert + Bonus, "folgt Anna");
                vorschlaege[carla] = new(Strafe, "zahlt fuer Anna");
                vorschlaege[anna] = new(Strafe, "zahlt Eintritt");
                return vorschlaege;
            }
        }

        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        [TestMethod]
        public async Task ACardThatNobodyTouchesFollowsTheRatingOfAnother()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 3);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            PlayerResultContext Karte(int i) => vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world.Players[i].Id);
            var (anna, bert, carla) = (Karte(0), Karte(1), Karte(2));
            var minus = world.Coordinate.CurrentMinusPoints;

            var regel = new FolgtAnnaRegel(anna.Player.Id, bert.Player.Id, carla.Player.Id);
            regel.Schliesse(new Dictionary<Guid, object>());
            vm.SetzeSonderregel(regel);

            // --- Bert falsch, Anna noch nicht bewertet: die Regel greift nicht ---
            bert.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-minus, bert.CurrentScoreManipulation);
            Assert.AreEqual(ScoreSuggestion.None, carla.Suggestion);

            // --- Anna richtig: Berts und Carlas Karte wechseln, ohne dass jemand sie anfasst ---
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(-minus + Bonus, bert.CurrentScoreManipulation, "Berts Karte sieht Annas Bewertung nicht.");
            StringAssert.Contains(bert.SuggestionText, "folgt Anna");
            Assert.AreEqual(ScoreSuggestion.Correction, carla.Suggestion, "Die unbewertete Karte traegt die Bewegung nicht.");
            Assert.AreEqual(Strafe, carla.CurrentScoreManipulation);

            // --- Anna doch falsch: alles zurueck auf die eigene Bewertung ---
            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-minus, bert.CurrentScoreManipulation, "Berts Karte bleibt auf der Regel stehen.");
            Assert.IsNull(bert.Grund);
            Assert.AreEqual(ScoreSuggestion.None, carla.Suggestion, "Carla bleibt auf der Bewegung stehen.");
            Assert.AreEqual(0, carla.CurrentScoreManipulation);

            // --- Ein eigener Wert von Hand bleibt, was auch immer Anna wird (E1) ---
            bert.Suggestion = ScoreSuggestion.Correction;
            bert.CurrentScoreManipulation = 42;
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(42, bert.CurrentScoreManipulation, "Die Regel ueberschreibt den eigenen Wert des Spielleiters.");
        }

        /// <summary>Gebucht wird, was auf der Karte steht - auch ein Minus unter „richtig" - und das Buchen wertet nicht neu.</summary>
        [TestMethod]
        public async Task BookingKeepsEveryCardAsShownAndCountsTheRating()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 3);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(int i) => karten.Single(k => k.Player.Id == world.Players[i].Id);
            var (anna, bert, carla) = (Karte(0), Karte(1), Karte(2));
            var minus = world.Coordinate.CurrentMinusPoints;

            var regel = new FolgtAnnaRegel(anna.Player.Id, bert.Player.Id, carla.Player.Id);
            regel.Schliesse(new Dictionary<Guid, object>());
            vm.SetzeSonderregel(regel);

            bert.Suggestion = ScoreSuggestion.Wrong;
            anna.Suggestion = ScoreSuggestion.Right;
            var erwartet = karten.ToDictionary(k => k.Player.Id, k => k.CurrentScoreManipulation);

            // Anna zuerst: wertete das Buchen neu, fielen Bert und Carla auf ihre eigene Bewertung zurueck.
            foreach (var karte in new[] { anna, bert, carla })
                karte.SetManipulationToResult();

            foreach (var karte in karten)
                Assert.AreEqual(erwartet[karte.Player.Id], karte.Result.FinalScore, $"{karte.Player.Designation}: gebucht ist nicht, was die Karte zeigte.");

            Assert.AreEqual(1, bert.Result.WrongCount, "Berts falsche Antwort ist nicht gezaehlt.");
            Assert.AreEqual(-minus + Bonus, bert.Result.FinalScore);
            Assert.AreEqual(1, carla.Result.CorrectionsCount, "Carlas Bewegung ging nicht als eigener Wert ein.");
            Assert.AreEqual(1, anna.Result.RightCount, "Annas richtige Antwort mit Minus ist nicht als richtig gezaehlt.");
        }
    }
}
