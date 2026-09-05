using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.StaticRessources;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace Quizzer.UnitTests.Views.BuzzerViews
{
    /// <summary>
    /// Die Uebersicht, die der Spielleiter waehrend einer laufenden Runde sieht: wer verbunden
    /// ist, wer schon abgegeben hat und was richtig waere.
    /// </summary>
    [TestClass]
    public class BuzzerLiveOverviewUnitTests
    {
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;

        [TestInitialize]
        public void SetUp()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };

            game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel", Phase = 1 };

            foreach (var player in new[] { anna, bert })
            {
                game.PlayerXGames.Add(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    PlayerId = player.Id,
                    Player = player,
                });
            }
        }

        [TestCleanup]
        public void TearDown() => StaticManager.Reset();

        private BuzzerControlsViewModel Open(QuestionBase question)
        {
            var serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = game;

            var vm = new BuzzerControlsViewModel();
            vm.SetBuzzerVerverViewModel(serverVm);
            vm.SetRoundInfo(question);

            return vm;
        }

        private static AppreciateQestion Appreciate() => new()
        {
            Id = Guid.NewGuid(),
            Designation = "Grossglockner",
            ValueKind = AppreciateValueKind.Length,
            Unit = AppreciateUnit.Meter,
            ExpectedValue = 3798,
        };

        private static MultipleChoiceQuestion MultipleChoice()
        {
            var question = new MultipleChoiceQuestion { Id = Guid.NewGuid(), Designation = "Hauptstadt" };

            question.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionViewKey = "A",
                Designation = "Graz",
                SequenceNumber = 10,
            });

            question.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionViewKey = "B",
                Designation = "Wien",
                SequenceNumber = 20,
                IsResult = true,
            });

            return question;
        }

        [TestMethod]
        public void EveryPlayerGetsALine()
        {
            var vm = Open(Appreciate());

            Assert.AreEqual(2, vm.LivePlayers.Count,
                "Der Spielleiter muss jeden Mitspieler sehen, nicht nur die verbundenen.");

            CollectionAssert.AreEquivalent(
                new[] { "Anna", "Bert" },
                vm.LivePlayers.Select(p => p.DisplayName).ToArray());
        }

        [TestMethod]
        public void BeforeAnyoneAnswers_EveryoneIsWaiting()
        {
            var vm = Open(Appreciate());

            foreach (var entry in vm.LivePlayers)
            {
                Assert.IsFalse(entry.HasAnswered);
                Assert.AreEqual("nicht verbunden", entry.StatusText,
                    "Ohne verbundenes Telefon ist das der ehrlichere Zustand als 'wartet'.");
            }

            anna.ConnectionState = PlayerConnection.Connected;
            vm.LivePlayers.First(p => p.PlayerId == anna.Id).RaiseConnectionChanged();

            Assert.AreEqual("wartet",
                vm.LivePlayers.First(p => p.PlayerId == anna.Id).StatusText);
        }

        [TestMethod]
        public void AGuessShowsUpAtTheRightPlayer()
        {
            var vm = Open(Appreciate());
            anna.ConnectionState = PlayerConnection.Connected;

            vm.OnPlayerSubmittedInput(new InputResult
            {
                PlayerId = anna.Id,
                Player = anna,
                Value = "3750",
                CommittedResult = true,
            });

            var annaLine = vm.LivePlayers.First(p => p.PlayerId == anna.Id);
            var bertLine = vm.LivePlayers.First(p => p.PlayerId == bert.Id);

            Assert.AreEqual("3750", annaLine.AnswerText);
            Assert.AreEqual("abgegeben", annaLine.StatusText);
            Assert.IsFalse(bertLine.HasAnswered, "Auf Bert wird noch gewartet.");
        }

        [TestMethod]
        public void SelectedKeysShowUp()
        {
            var vm = Open(MultipleChoice());
            anna.ConnectionState = PlayerConnection.Connected;

            vm.OnPlayerSelectedKeys(new SelectionResult
            {
                PlayerId = anna.Id,
                Player = anna,
                SelectedKeys = new List<string> { "B" },
                CommittedResult = true,
            });

            Assert.AreEqual("B", vm.LivePlayers.First(p => p.PlayerId == anna.Id).AnswerText);
        }

        [TestMethod]
        public void TheBuzzerWinnerIsMarked()
        {
            var vm = Open(new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Frage" });

            bert.ConnectionState = PlayerConnection.Connected;
            vm.OnBuzzerWinner(bert, 1);

            Assert.AreEqual("gebuzzert", vm.LivePlayers.First(p => p.PlayerId == bert.Id).StatusText);
            Assert.IsFalse(vm.LivePlayers.First(p => p.PlayerId == anna.Id).IsWinner);
        }

        [TestMethod]
        public void ANewRoundClearsTheAnswers()
        {
            var vm = Open(Appreciate());
            anna.ConnectionState = PlayerConnection.Connected;

            vm.OnPlayerSubmittedInput(new InputResult
            {
                PlayerId = anna.Id,
                Player = anna,
                Value = "3750",
                CommittedResult = true,
            });

            var annaLine = vm.LivePlayers.First(p => p.PlayerId == anna.Id);

            // Erst festhalten, dass der Tipp ueberhaupt ankam: sonst waere dieser Test auch
            // gruen, wenn nie etwas eingetragen wird - dann prueft er eine Abwesenheit.
            Assert.AreEqual("3750", annaLine.AnswerText,
                "Ohne angekommenen Tipp misst der Rest dieses Tests nichts.");

            vm.OnReset(2);

            Assert.IsFalse(annaLine.HasAnswered,
                "Nach dem Zuruecksetzen darf der alte Tipp nicht stehen bleiben.");
            Assert.AreEqual("wartet", annaLine.StatusText);
        }

        [TestMethod]
        public void TheSolutionIsSpelledOut()
        {
            Assert.AreEqual("3798 m", Open(Appreciate()).SolutionText);
            Assert.AreEqual("B: Wien", Open(MultipleChoice()).SolutionText);
        }

        [TestMethod]
        public void WithoutASolutionTheLineStaysAway()
        {
            var vm = Open(new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Frage" });

            Assert.AreEqual(string.Empty, vm.SolutionText,
                "Eine Standardfrage hat keine feste Loesung - dann bleibt die Zeile leer.");
            Assert.AreEqual(System.Windows.Visibility.Collapsed, vm.SolutionVisibility);
        }

        [TestMethod]
        public void TheHeadlineNamesTypeAndRound()
        {
            game.CurrentRound = 3;

            StringAssert.Contains(Open(Appreciate()).RoundHeadline, "Schätzfrage");
            StringAssert.Contains(Open(Appreciate()).RoundHeadline, "3");
        }
    }
}
