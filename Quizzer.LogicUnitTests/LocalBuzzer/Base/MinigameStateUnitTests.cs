using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Der Minispiel-Rahmen am Server (M35 B3) ohne Netz: jede Abgabe kommt aus dem WLAN und wird
    /// vor der Art geprueft - Phase, Art, Durchgang, Sperre je Spieler, Doppelabgabe.
    /// </summary>
    [TestClass]
    public class MinigameStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private MinigameState spiel = null!;
        private readonly List<(int Von, int Bis)> gewuerfelt = new();

        [TestInitialize]
        public void Aufbau()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", ConnectionState = PlayerConnection.Connected };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", ConnectionState = PlayerConnection.Connected };

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = new MinigameState(new GameAccessor { GetGame = () => game })
            {
                Zufall = (von, bis) => { gewuerfelt.Add((von, bis)); return 3456; },
            };
            spiel.Configure(new MinigameSetup(MinigameKind.Reaction, "Reaktion", "Regel", TotalRounds: 2));
            spiel.Reset();
        }

        private MinigameAction Aktion(object nutzlast, int? durchgang = null, MinigameKind art = MinigameKind.Reaction) => new()
        {
            Kind = art,
            Durchgang = durchgang ?? spiel.Durchgang,
            Payload = JsonSerializer.SerializeToElement(nutzlast),
        };

        private MinigameAction Zeit(int ms) => Aktion(new { falseStart = false, milliseconds = ms });

        private ReactionClientInfo ArtFuer(Player p) => (ReactionClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        [TestMethod]
        public void NothingIsTakenBeforeTheRoundStarts()
        {
            Assert.IsFalse(spiel.Submit(anna, Zeit(300)).Accepted, "Vor dem Start der Runde wurde eine Abgabe angenommen.");
            Assert.IsTrue(spiel.IsLockedFor(anna), "Vor dem Start ist das Telefon offen.");
        }

        [TestMethod]
        public void TheServerRollsTheWaitBetweenTwoAndSixSeconds()
        {
            spiel.StartNextRound();

            CollectionAssert.AreEqual(new[] { (ReaktionEvaluator.MinWaitMs, ReaktionEvaluator.MaxWaitMs + 1) }, gewuerfelt);
            Assert.AreEqual(3456, ArtFuer(anna).WaitMs, "Die gewuerfelte Wartezeit kommt nicht beim Telefon an.");

            var echt = new MinigameState(new GameAccessor());
            echt.Configure(new MinigameSetup(MinigameKind.Reaction, "R", "", 1));

            for (var i = 0; i < 50; i++)
            {
                echt.Reset();
                echt.StartNextRound();
                var ms = ((ReactionRules)echt.Rules!).WaitMs;
                Assert.IsTrue(ms is >= 2000 and <= 6000, $"Wartezeit {ms} ms ausserhalb von 2-6 s.");
            }
        }

        [TestMethod]
        public void ASubmissionCountsOnceAndOnlyInTheRunningDurchgang()
        {
            spiel.StartNextRound();
            var alterDurchgang = spiel.Durchgang - 1;

            Assert.IsFalse(spiel.Submit(anna, Aktion(new { falseStart = false, milliseconds = 300 }, alterDurchgang)).Accepted, "Eine Abgabe aus einem alten Durchgang zaehlt.");
            Assert.IsFalse(spiel.Submit(anna, Aktion(new { falseStart = false, milliseconds = 300 }, art: MinigameKind.Majority)).Accepted,
                "Eine Abgabe fuer eine andere Art zaehlt.");
            Assert.IsFalse(spiel.Submit(anna, Aktion(new { milliseconds = (int?)null })).Accepted, "Eine leere Nutzlast zaehlt.");
            Assert.IsFalse(spiel.Submit(anna, Aktion("kaputt")).Accepted, "Eine kaputte Nutzlast zaehlt.");

            var erste = spiel.Submit(anna, Zeit(300));
            Assert.IsTrue(erste.Accepted);
            Assert.AreEqual(new ReactionAttempt(ReactionOutcome.Valid, 300), erste.Value);

            Assert.IsFalse(spiel.Submit(anna, Zeit(150)).Accepted, "Eine zweite Abgabe in derselben Runde zaehlt.");
            Assert.AreEqual(300, ArtFuer(anna).OwnMilliseconds, "Die zweite Abgabe hat die erste ueberschrieben.");
            Assert.IsTrue(spiel.IsLockedFor(anna), "Nach der Abgabe ist das Telefon noch offen.");
            Assert.IsFalse(spiel.IsLockedFor(bert), "Annas Abgabe sperrt Bert.");
        }

        [TestMethod]
        public void TheRoundClosesWhenAllConnectedPlayersSubmitted()
        {
            spiel.StartNextRound();

            Assert.IsNull(spiel.Submit(anna, Zeit(300)).Closed, "Die Runde schliesst, obwohl Bert fehlt.");

            var zu = spiel.Submit(bert, Aktion(new { falseStart = true })).Closed;

            Assert.IsNotNull(zu, "Nach der letzten Abgabe bleibt die Runde offen.");
            Assert.AreEqual(1, zu.Round);
            Assert.IsFalse(zu.Finished);
            Assert.AreEqual(MinigamePhase.RoundOver, spiel.Phase);
            Assert.AreEqual(2, zu.Submissions.Count);
        }

        [TestMethod]
        public void ADisconnectedPhoneSubmitsNothingAndDoesNotHoldTheRound()
        {
            spiel.StartNextRound();
            spiel.Submit(anna, Zeit(300));

            bert.ConnectionState = PlayerConnection.Disconnected;
            var zu = spiel.CloseIfComplete();

            Assert.IsNotNull(zu, "Ein getrenntes Telefon haelt die Runde fest.");
            Assert.IsFalse(zu.Submissions.ContainsKey(bert.Id), "Fuer das getrennte Telefon steht eine Abgabe da.");
        }

        [TestMethod]
        public void ALockedPlayerCannotSubmitAndIsNotWaitedFor()
        {
            spiel.LockPlayer(bert.Id);
            spiel.StartNextRound();

            Assert.IsTrue(spiel.IsLockedFor(bert), "Der gesperrte Spieler ist offen.");
            Assert.IsFalse(spiel.IsLockedFor(anna), "Die Sperre trifft auch Anna.");
            Assert.IsFalse(spiel.Submit(bert, Zeit(200)).Accepted, "Der gesperrte Spieler hat abgegeben.");

            Assert.IsNotNull(spiel.Submit(anna, Zeit(300)).Closed, "Auf den gesperrten Spieler wird gewartet.");

            spiel.UnlockPlayer(bert.Id);
            Assert.IsFalse(spiel.IsPlayerLocked(bert.Id));
        }

        [TestMethod]
        public void TheGameMasterRepeatsClosesAndAborts()
        {
            spiel.StartNextRound();
            spiel.Submit(anna, Zeit(300));

            Assert.IsTrue(spiel.RepeatRound());
            Assert.AreEqual(1, spiel.Round, "Die Wiederholung zaehlt die Runde weiter.");
            Assert.IsNull(ArtFuer(anna).OwnOutcome, "Nach der Wiederholung steht die alte Abgabe noch da.");
            Assert.IsTrue(spiel.Submit(anna, Zeit(280)).Accepted, "In der Wiederholung kann man nicht neu abgeben.");

            var zu = spiel.CloseRound();
            Assert.IsNotNull(zu, "Der Spielleiter kann die Runde nicht schliessen, obwohl Bert fehlt.");
            Assert.IsFalse(zu.Submissions.ContainsKey(bert.Id));

            Assert.IsTrue(spiel.StartNextRound());
            spiel.Submit(anna, Zeit(250));
            spiel.Submit(bert, Zeit(260));

            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase, "Nach der letzten Runde ist das Spiel nicht vorbei.");
            Assert.IsFalse(spiel.StartNextRound(), "Nach der letzten Runde startet noch eine.");
            Assert.IsTrue(ArtFuer(anna).IsWinner, "Am Ende erfaehrt der Sieger es nicht.");
            CollectionAssert.AreEqual(new[] { "Anna" }, ArtFuer(bert).WinnerNames);

            var abbruch = spiel.Abort();
            Assert.IsTrue(abbruch!.Aborted);
            Assert.IsFalse(spiel.RepeatRound(), "Nach dem Abbruch laesst sich noch wiederholen.");
        }
    }
}
