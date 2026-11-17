using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Base.States.Sonderregeln;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Vorphase des Finales am Server (M37 E4) ohne Netz - die Missbrauchsfälle des Auftrags: ein Einsatz über der eigenen
    /// Obergrenze oder negativ, eine doppelte Abgabe, eine Abgabe für den Einsatz statt fürs Finale. Jeder setzt von seinem
    /// eigenen Kontostand (<c>EinsatzRegel.Obergrenze</c>) und sieht nur den eigenen Einsatz.
    /// </summary>
    [TestClass]
    public class FinaleVorphaseUnitTests
    {
        private const int Feld = 100;

        private Player anna = null!;
        private Player bert = null!;
        private MinigameState zustand = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            zustand = new MinigameState(new GameAccessor { GetGame = () => game });

            // Anna hat 400 auf dem Konto, Bert 30 - seine Grenze sind die Feldpunkte.
            zustand.Configure(new MinigameSetup(default, "Sonderregel: Finale", "Regel", 1, CountdownSeconds: 0, SpecialRule: CellSpecialRule.Final,
                FieldPoints: Feld, Balances: new Dictionary<Guid, int> { [anna.Id] = 400, [bert.Id] = 30 }));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Vorphase des Finales liess sich nicht starten.");
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private MinigameSubmitResult Gib(Player p, object nutzlast, CellSpecialRule regel = CellSpecialRule.Final)
            => zustand.Submit(p, new MinigameAction
            {
                SpecialRule = regel,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private StakeClientInfo Info(Player p) => (StakeClientInfo)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        [TestMethod]
        public void TheFinalPlaysTheStakePhaseUnderItsOwnRule()
        {
            Assert.IsInstanceOfType<StakeRules>(zustand.Rules, "Das Finale spielt keine Einsatz-Vorphase.");
            Assert.AreEqual(CellSpecialRule.Final, ((StakeRules)zustand.Rules!).Rule);
            Assert.AreEqual(CellSpecialRule.Final, ((MinigameClientInfo)zustand.InfoFor(anna)).SpecialRule, "Das Telefon erfährt das Finale nicht.");
        }

        [TestMethod]
        public void AStakeAboveTheOwnLimitOrBelowZeroIsRejected()
        {
            Assert.AreEqual(400, Info(anna).Max, "Annas Grenze ist ihr eigener Kontostand.");
            Assert.AreEqual(Feld, Info(bert).Max, "Berts Grenze sind die Feldpunkte.");

            Assert.IsFalse(Gib(bert, new { amount = Feld + 1 }).Accepted, "Über der Grenze angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = -1 }).Accepted, "Ein negativer Einsatz angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = 400 }).Accepted, "Bert setzt mit Annas Kontostand.");
            Assert.IsFalse(Gib(bert, new { amount = int.MaxValue }).Accepted, "Ein riesiger Einsatz angenommen.");

            Assert.IsTrue(Gib(bert, new { amount = Feld }).Accepted, "Genau die Grenze abgewiesen.");
            Assert.IsTrue(Gib(anna, new { amount = 400 }).Accepted);
        }

        [TestMethod]
        public void ASecondStakeAndAStakeForAnotherRuleAreRejected()
        {
            Assert.IsFalse(Gib(anna, new { amount = 50 }, CellSpecialRule.Stake).Accepted, "Eine Abgabe für den Einsatz zählt im Finale.");
            Assert.IsTrue(Gib(anna, new { amount = 50 }).Accepted);
            Assert.IsFalse(Gib(anna, new { amount = 60 }).Accepted, "Die zweite Abgabe wurde angenommen.");
            Assert.AreEqual(50, Info(anna).OwnAmount, "Die erste Abgabe wurde überschrieben.");
        }

        [TestMethod]
        public void NoOtherPlayerSeesAStake()
        {
            Gib(anna, new { amount = 377 });

            Assert.IsNull(Info(bert).OwnAmount, "Bert sieht Annas Einsatz.");
            Assert.IsFalse(Zustandsinhalt.Von(zustand.InfoFor(bert)).Zahlen.Contains(377), "Annas Einsatz steht in Berts Zustand.");
            Assert.IsFalse(Zustandsinhalt.Von(zustand.InfoFor(null)).Zahlen.Contains(377), "Annas Einsatz steht im allgemeinen Zustand.");
            Assert.AreEqual(377, Info(anna).OwnAmount, "Anna bekommt ihren Einsatz nicht zurück.");
        }
    }
}
