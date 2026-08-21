using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base.States
{
    /// <summary>
    /// Wer zuerst drueckt, gewinnt. Der Wettlauf ist die eigentliche Zusage des Buzzers -
    /// er muss auch dann halten, wenn zwei Telefone im selben Augenblick senden.
    /// </summary>
    [TestClass]
    public class BuzzerStateUnitTests
    {
        private static BuzzerState NewState() => new(new GameAccessor());

        private static Player NewPlayer(string name) => new() { Id = Guid.NewGuid(), Designation = name };

        [TestMethod]
        public void TryBuzz_FirstPlayerWins()
        {
            var state = NewState();
            var anna = NewPlayer("Anna");

            Assert.IsTrue(state.TryBuzz(anna));
            Assert.AreEqual(anna, state.Winner);
            Assert.IsTrue(state.Locked);
        }

        [TestMethod]
        public void TryBuzz_SecondPlayerIsRejectedAndDoesNotOverwriteTheWinner()
        {
            var state = NewState();
            var anna = NewPlayer("Anna");
            var bert = NewPlayer("Bert");
            state.TryBuzz(anna);

            Assert.IsFalse(state.TryBuzz(bert));
            Assert.AreEqual(anna, state.Winner);
        }

        [TestMethod]
        public void TryBuzz_WithoutPlayer_IsRejectedAndLeavesTheStateOpen()
        {
            var state = NewState();

            Assert.IsFalse(state.TryBuzz(null));
            Assert.IsFalse(state.Locked, "Ein Fehlgriff darf die Runde nicht sperren.");
        }

        [TestMethod]
        public void Reset_OpensTheRoundAgain()
        {
            var state = NewState();
            state.TryBuzz(NewPlayer("Anna"));

            state.Reset();

            Assert.IsFalse(state.Locked);
            Assert.IsNull(state.Winner);
            Assert.IsTrue(state.TryBuzz(NewPlayer("Bert")));
        }

        [TestMethod]
        public void ClearAndLock_BlocksEveryoneWithoutNamingAWinner()
        {
            var state = NewState();

            state.ClearAndLock();

            Assert.IsTrue(state.Locked);
            Assert.IsNull(state.Winner);
            Assert.IsFalse(state.TryBuzz(NewPlayer("Anna")));
        }

        [TestMethod]
        public void TryBuzz_UnderConcurrency_ExactlyOnePlayerWins()
        {
            var state = NewState();
            var players = Enumerable.Range(0, 64).Select(i => NewPlayer($"Spieler{i}")).ToArray();
            var winners = 0;

            Parallel.ForEach(players, player =>
            {
                if (state.TryBuzz(player))
                    Interlocked.Increment(ref winners);
            });

            Assert.AreEqual(1, winners, "64 gleichzeitige Buzzer, genau ein Gewinner.");
            Assert.IsNotNull(state.Winner);
        }
    }
}
