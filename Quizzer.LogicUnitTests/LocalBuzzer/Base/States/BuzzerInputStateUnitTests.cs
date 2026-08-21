using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base.States
{
    /// <summary>
    /// Das Eingabe-Layout der Schaetzfrage. Entscheidend ist, wann die Runde schliesst:
    /// erst wenn alle Mitspieler abgegeben haben, sonst kommt der Letzte nie dazu.
    /// </summary>
    [TestClass]
    public class BuzzerInputStateUnitTests
    {
        private static Player NewPlayer(string name) => new() { Id = Guid.NewGuid(), Designation = name };

        /// <summary>Baut Zustand und Spieler; der GameAccessor liefert ein Spiel mit diesen Spielern.</summary>
        private static (BuzzerInputState State, List<Player> Players) StateWith(int playerCount)
        {
            var players = Enumerable.Range(1, playerCount)
                .Select(i => NewPlayer($"Spieler {i}")).ToList();

            var game = new Game { Id = Guid.NewGuid() };

            foreach (var player in players)
            {
                game.PlayerXGames.Add(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    PlayerId = player.Id,
                    Player = player,
                });
            }

            var accessor = new GameAccessor { GetGame = () => game };

            return (new BuzzerInputState(accessor), players);
        }

        private static BuzzerInputState.InputResult Input(Player player, string value)
            => new() { PlayerId = player.Id, Player = player, Value = value };

        [TestMethod]
        public void TheLayoutAnnouncesItself()
        {
            var (state, _) = StateWith(2);

            Assert.AreEqual(BuzzerControlsLayout.Input, state.BuzzerControlsLayout);
        }

        [TestMethod]
        public void AGuessIsRecorded()
        {
            var (state, players) = StateWith(2);

            state.SetInput(Input(players[0], "3798"));

            Assert.AreEqual(1, state.InputsForPlayer.Count);
            Assert.AreEqual("3798", state.InputsForPlayer[players[0].Id].Value);
            Assert.IsTrue(state.InputsForPlayer[players[0].Id].CommittedResult);
        }

        [TestMethod]
        public void TheRoundStaysOpenUntilEveryoneHasAnswered()
        {
            var (state, players) = StateWith(3);

            state.SetInput(Input(players[0], "100"));
            Assert.IsFalse(state.Locked);

            state.SetInput(Input(players[1], "200"));
            Assert.IsFalse(state.Locked, "Solange einer fehlt, bleibt offen.");

            state.SetInput(Input(players[2], "300"));
            Assert.IsTrue(state.Locked);
        }

        /// <summary>
        /// Sonst schliesst die Runde, sobald jemand versehentlich auf Bestaetigen tippt,
        /// und niemand kann mehr nachlegen.
        /// </summary>
        [TestMethod]
        public void AnEmptyGuessDoesNotCountAsAnAnswer()
        {
            var (state, players) = StateWith(2);

            state.SetInput(Input(players[0], "   "));
            state.SetInput(Input(players[1], "500"));

            Assert.IsFalse(state.Locked);
            Assert.IsFalse(state.InputsForPlayer[players[0].Id].CommittedResult);
        }

        [TestMethod]
        public void ASecondGuessReplacesTheFirst()
        {
            var (state, players) = StateWith(2);

            state.SetInput(Input(players[0], "100"));
            state.SetInput(Input(players[0], "250"));

            Assert.AreEqual(1, state.InputsForPlayer.Count);
            Assert.AreEqual("250", state.InputsForPlayer[players[0].Id].Value);
        }

        [TestMethod]
        public void OnceLockedNothingMoreIsTakenIn()
        {
            var (state, players) = StateWith(1);

            state.SetInput(Input(players[0], "100"));
            Assert.IsTrue(state.Locked);

            state.SetInput(Input(players[0], "999"));

            Assert.AreEqual("100", state.InputsForPlayer[players[0].Id].Value,
                "Nach dem Schliessen darf niemand mehr nachbessern.");
        }

        [TestMethod]
        public void WithoutAPlayerNothingIsRecorded()
        {
            var (state, _) = StateWith(2);

            state.SetInput(new BuzzerInputState.InputResult { Value = "100" });

            Assert.AreEqual(0, state.InputsForPlayer.Count);
            Assert.IsFalse(state.Locked);
        }

        [TestMethod]
        public void WithoutAGameTheRoundClosesImmediately()
        {
            var state = new BuzzerInputState(new GameAccessor());
            var player = NewPlayer("Allein");

            state.SetInput(Input(player, "100"));

            Assert.IsTrue(state.Locked,
                "Ohne bekannte Spielerzahl laesst sich kein Ende bestimmen - dann lieber zu.");
        }

        [TestMethod]
        public void ResetOpensANewRound()
        {
            var (state, players) = StateWith(1);
            state.SetInput(Input(players[0], "100"));

            state.Reset();

            Assert.IsFalse(state.Locked);
            Assert.AreEqual(0, state.InputsForPlayer.Count);
        }

        [TestMethod]
        public void ClearAndLockAlsoDropsTheFieldSettings()
        {
            var (state, players) = StateWith(2);
            state.Infos = new BuzzerInputState.BuzzerInputInfo
            { InputType = "date", Placeholder = "Datum" };
            state.SetInput(Input(players[0], "100"));

            state.ClearAndLock();

            Assert.IsTrue(state.Locked);
            Assert.AreEqual(0, state.InputsForPlayer.Count);
            Assert.AreEqual("text", state.Infos.InputType);
        }

        [TestMethod]
        public void LockAllCountsWhatIsAlreadyThere()
        {
            var (state, players) = StateWith(3);
            state.SetInput(Input(players[0], "100"));

            state.LockAll();

            Assert.IsTrue(state.Locked);
            Assert.IsTrue(state.InputsForPlayer[players[0].Id].CommittedResult,
                "Der Spielleiter bricht ab - was da ist, zaehlt.");
        }

        /// <summary>
        /// Die Feldnamen muessen zu dem passen, was inputLayout.js ausliest
        /// (<c>info.inputType</c>, <c>info.placeholder</c>).
        /// </summary>
        [TestMethod]
        public void TheStateInfoCarriesInputTypeAndPlaceholder()
        {
            var (state, _) = StateWith(2);
            state.Infos = new BuzzerInputState.BuzzerInputInfo
            {
                InputType = "number",
                Placeholder = "Wert in km",
                QuestionId = Guid.NewGuid(),
            };

            var info = state.BuzzerStateInfo as BuzzerInputState.BuzzerInputInfo;

            Assert.IsNotNull(info);
            Assert.AreEqual("number", info.InputType);
            Assert.AreEqual("Wert in km", info.Placeholder);
        }
    }
}
