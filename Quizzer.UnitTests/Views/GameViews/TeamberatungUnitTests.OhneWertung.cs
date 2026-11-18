using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Ohne Wertung" von Hand (Knopf, Strg+0) auf der Karte eines Mitglieds (R4-5): eine Entscheidung der Spielleitung - der Anteil
    /// des Teams kommt nicht wieder, auch nicht bei einer neuen Wertung des Sprechers, bis die Spielleitung die Karte selbst bewertet.
    /// </summary>
    public partial class TeamberatungUnitTests
    {
        [TestMethod]
        public async Task NoRatingByHandOnAMembersCardStays()
        {
            var vm = await OeffneTeamberatungAsync();
            var (anna, bert) = (Karte(vm, 0), Karte(vm, 1));
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreNotEqual(0, bert.CurrentScoreManipulation, "Bert hat keinen Anteil - dann misst der Test nichts.");

            vm.PlayersResultViewModel!.Select(bert);
            vm.PlayersResultViewModel.QuickNoneCommand.Execute(null);

            Assert.AreEqual((ScoreSuggestion.None, 0), (bert.Suggestion, bert.CurrentScoreManipulation), "Strg+0 legt Berts Anteil sofort wieder hin.");

            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual((ScoreSuggestion.None, 0), (bert.Suggestion, bert.CurrentScoreManipulation),
                "Die neue Wertung der Sprecherin überschreibt Berts „ohne Wertung“.");

            bert.WrongSuggestionCommand.Execute(null);
            Assert.AreEqual(anna.CurrentScoreManipulation + bert.CurrentScoreManipulation, -world!.Coordinate.CurrentMinusPoints,
                "Bewertet die Spielleitung Bert wieder, gilt nicht der Anteil des Teams.");
        }

        /// <summary>
        /// Bewertet die Spielleitung die Karte nach „ohne Wertung“ selbst, ist die Entscheidung vorbei: steht die Karte nach dem
        /// Buchen wieder auf „unbewertet“, legt eine neue Wertung der Sprecherin den Anteil wieder hin.
        /// </summary>
        [TestMethod]
        public async Task ARatingByHandEndsTheNoRating()
        {
            var vm = await OeffneTeamberatungAsync();
            var (anna, bert) = (Karte(vm, 0), Karte(vm, 1));
            anna.Suggestion = ScoreSuggestion.Right;
            bert.NoneSuggestionCommand.Execute(null);
            bert.RightSuggestionCommand.Execute(null);
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Buchen scheiterte.");

            anna.Suggestion = ScoreSuggestion.Wrong;

            Assert.AreNotEqual(0, bert.CurrentScoreManipulation, "Berts „ohne Wertung“ gilt weiter, obwohl die Spielleitung ihn danach selbst bewertet hat.");
        }

        /// <summary>„Runde zurücksetzen“: die Entscheidung galt den alten Antworten - in der neuen Runde bekommt Bert wieder seinen Anteil.</summary>
        [TestMethod]
        public async Task ANewRoundForgetsTheNoRatingByHand()
        {
            var vm = await OeffneTeamberatungAsync();
            var (anna, bert) = (Karte(vm, 0), Karte(vm, 1));
            bert.NoneSuggestionCommand.Execute(null);

            await ((AsyncRelayCommand)vm.BuzzerControlsViewModel!.ResetRoundCommand).ExecuteAsync(vm.CurrentBuzzerLayout);
            anna.Suggestion = ScoreSuggestion.Right;

            Assert.AreNotEqual(0, bert.CurrentScoreManipulation, "Nach dem Zurücksetzen gilt Berts „ohne Wertung“ aus der alten Runde weiter.");
        }
    }
}
