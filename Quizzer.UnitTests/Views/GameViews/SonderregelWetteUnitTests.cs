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
    /// Wette auf den Karten (M35 C3b), ohne Server - die Vorphase pruefen die Server-Tests: wer wettet, traegt die
    /// verlorene Wette sofort; wird der Gewaehlte richtig, wechselt die Karte des Wettenden, ohne dass jemand sie anfasst.
    /// </summary>
    [TestClass]
    public class SonderregelWetteUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        [TestMethod]
        public async Task TheBettorsCardFollowsTheRatingOfTheChosenPlayer()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 3);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            PlayerResultContext Karte(int i) => vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world.Players[i].Id);
            var (anna, bert, clara) = (Karte(0), Karte(1), Karte(2));
            var feld = world.Coordinate.CurrentPoints;
            var einsatz = WetteRegel.Einsatz(feld, 50);
            var konten = world.Players.ToDictionary(p => p.Id, _ => 0);

            // Alle gleichauf: Rang 1 von 3, Gewinn = Einsatz * 4/3.
            var gewinn = WetteRegel.Gewinn(einsatz, 1, 3);

            var regel = SonderregelAblauf.Fuer(CellSpecialRule.Bet)!;
            regel.Oeffne(new VorphaseGrundlage(feld, konten, world.Players.ToDictionary(p => p.Id, p => p.CalculatedDisplayName), Anteil: 50));
            regel.Schliesse(new Dictionary<Guid, object> { [anna.Player.Id] = bert.Player.Id, [bert.Player.Id] = bert.Player.Id });
            vm.SetzeSonderregel(regel);
            vm.WerteSonderregel();

            // --- Gleich nach der Vorphase: wer gewettet hat, verliert, solange Bert nicht richtig ist ---
            Assert.AreEqual(-einsatz, anna.CurrentScoreManipulation, "Die verlorene Wette steht nicht gleich auf Annas Karte.");
            Assert.AreEqual(ScoreSuggestion.Correction, anna.Suggestion);
            Assert.AreEqual(0, clara.CurrentScoreManipulation, "Wer nicht wettet, traegt eine Bewegung.");
            StringAssert.Contains(anna.SonderregelText, $"Wette auf {bert.Player.CalculatedDisplayName}");
            Assert.AreEqual("Keine Wette", clara.SonderregelText);

            // --- Bert richtig: Annas Karte wechselt von selbst, Bert bekommt Feldpunkte und Gewinn ---
            bert.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(gewinn, anna.CurrentScoreManipulation, "Annas Karte sieht Berts richtige Antwort nicht.");
            StringAssert.Contains(anna.SuggestionText, "gewonnen");
            Assert.AreEqual(feld + gewinn, bert.CurrentScoreManipulation, "Bert bekommt nicht Feldpunkte und Gewinn.");

            // --- Bert doch falsch: beide verlieren die Wette, Bert dazu seine Minuspunkte ---
            bert.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-einsatz, anna.CurrentScoreManipulation, "Annas Karte bleibt auf dem Gewinn stehen.");
            Assert.AreEqual(-world.Coordinate.CurrentMinusPoints - einsatz, bert.CurrentScoreManipulation);
            Assert.AreEqual(0, clara.CurrentScoreManipulation);

            // --- R4-5: „Ohne Wertung“ von Hand auf Annas Karte - die Wette folgt Bert weiter (nur an Teamregeln bleibt es) ---
            anna.NoneSuggestionCommand.Execute(null);
            Assert.AreEqual(-einsatz, anna.CurrentScoreManipulation, "„Ohne Wertung“ von Hand nimmt Anna die verlorene Wette.");
        }
    }
}
