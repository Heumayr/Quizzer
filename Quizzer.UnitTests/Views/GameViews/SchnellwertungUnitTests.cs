using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// M11: Punkte mit weniger Klicks - Karte per Nummer waehlen, der Buzzer-Gewinner steht vorn,
    /// eine Taste setzt die Bewertung (gespeichert wird weiterhin erst mit Speichern).
    /// </summary>
    [TestClass]
    public class SchnellwertungUnitTests
    {
        private static (PlayersResultViewModel Vm, List<PlayerResultContext> Karten) Bewertung(params string[] namen)
        {
            var vm = new PlayersResultViewModel();
            var karten = namen.Select(n =>
            {
                var spieler = new Player { Id = Guid.NewGuid(), Designation = n, DisplayName = n };
                return new PlayerResultContext
                {
                    PlayersResultViewModel = vm,
                    Player = spieler,
                    Result = new QuestionResult { Id = Guid.NewGuid(), PlayerId = spieler.Id },
                };
            }).ToList();

            vm.PlayerResultContextList = karten;
            return (vm, karten);
        }

        [TestMethod]
        public void TheCardsAreNumberedAndTheFirstIsSelected()
        {
            UiTestHost.Run(() =>
            {
                var (vm, karten) = Bewertung("Anna", "Bert", "Carla");

                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, karten.Select(k => k.CardNumber).ToList());
                Assert.AreSame(karten[0], vm.SelectedContext);
                Assert.IsTrue(karten[0].IsSelected);
                StringAssert.StartsWith(karten[1].CardLabel, "2 ·");
            });
        }

        [TestMethod]
        public void TheBuzzerWinnerIsSelectedAndShownOnTop()
        {
            UiTestHost.Run(() =>
            {
                var (vm, karten) = Bewertung("Anna", "Bert", "Carla");

                vm.CurrentBuzzerWinner = karten[2].Player;

                Assert.AreSame(karten[2], vm.SelectedContext, "Wer gebuzzert hat, wird bewertet.");
                Assert.AreEqual(Visibility.Visible, vm.WinnerBarVisibility);
                StringAssert.Contains(vm.WinnerText, "Carla");
                Assert.IsFalse(karten[0].IsSelected, "Nur eine Karte ist gewählt.");
            });
        }

        [TestMethod]
        public void WithoutABuzzerWinnerThereIsNoBar()
        {
            UiTestHost.Run(() =>
            {
                var (vm, _) = Bewertung("Anna");

                Assert.AreEqual(Visibility.Collapsed, vm.WinnerBarVisibility);
            });
        }

        [TestMethod]
        public void AKeySetsTheRatingOfTheSelectedCardOnly()
        {
            UiTestHost.Run(() =>
            {
                var (vm, karten) = Bewertung("Anna", "Bert");

                vm.SelectCardCommand.Execute("2");
                vm.QuickWrongCommand.Execute(null);

                Assert.AreEqual(PlayerResultContext.ScoreSuggestion.Wrong, karten[1].Suggestion);
                Assert.AreEqual(PlayerResultContext.ScoreSuggestion.None, karten[0].Suggestion, "Die andere Karte bleibt unberührt.");

                vm.QuickNoneCommand.Execute(null);
                Assert.AreEqual(PlayerResultContext.ScoreSuggestion.None, karten[1].Suggestion, "Strg+0 nimmt einen Vertipper zurück.");
            });
        }

        [TestMethod]
        public void TheBarActsOnTheWinnerEvenIfAnotherCardWasSelected()
        {
            UiTestHost.Run(() =>
            {
                var (vm, karten) = Bewertung("Anna", "Bert");
                vm.CurrentBuzzerWinner = karten[0].Player;
                vm.SelectCardCommand.Execute("2");

                vm.WinnerRightCommand.Execute(null);

                Assert.AreEqual(PlayerResultContext.ScoreSuggestion.Right, karten[0].Suggestion);
                Assert.AreEqual(PlayerResultContext.ScoreSuggestion.None, karten[1].Suggestion);
                Assert.AreSame(karten[0], vm.SelectedContext);
            });
        }

        [TestMethod]
        public void ANumberBeyondTheCardsChangesNothing()
        {
            UiTestHost.Run(() =>
            {
                var (vm, karten) = Bewertung("Anna", "Bert");

                vm.SelectCardCommand.Execute("7");

                Assert.AreSame(karten[0], vm.SelectedContext);
            });
        }
    }
}
