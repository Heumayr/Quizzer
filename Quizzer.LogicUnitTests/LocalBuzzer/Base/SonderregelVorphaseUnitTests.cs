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
    /// Die Vorphase der Sonderregeln am Server (M35 C3) ohne Netz: sie laeuft im Minispiel-Rahmen, jede
    /// Abgabe kommt aus dem WLAN - Grenze des Einsatzes, fremde Regel, Doppelabgabe, kaputte Nutzlast.
    /// </summary>
    [TestClass]
    public class SonderregelVorphaseUnitTests
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
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        /// <summary>Anna hat 400 auf dem Konto, Bert 30 - seine Grenze sind die Feldpunkte.</summary>
        private void Starte(CellSpecialRule regel)
        {
            zustand.Configure(new MinigameSetup(default, "Sonderregel", "Regel", 1, CountdownSeconds: 0, SpecialRule: regel,
                FieldPoints: Feld, Balances: new Dictionary<Guid, int> { [anna.Id] = 400, [bert.Id] = 30 }));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Vorphase liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast, CellSpecialRule regel, int? durchgang = null)
            => zustand.Submit(p, new MinigameAction
            {
                SpecialRule = regel,
                Durchgang = durchgang ?? zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private T Info<T>(Player p) => (T)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        [TestMethod]
        public void TheStakeIsCheckedAgainstEachPlayersOwnLimit()
        {
            Starte(CellSpecialRule.Stake);

            Assert.AreEqual(400, Info<StakeClientInfo>(anna).Max, "Annas Grenze ist ihr Kontostand.");
            Assert.AreEqual(Feld, Info<StakeClientInfo>(bert).Max, "Berts Grenze sind die Feldpunkte.");

            Assert.IsFalse(Gib(bert, new { amount = Feld + 1 }, CellSpecialRule.Stake).Accepted, "Ueber der Grenze angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = -5 }, CellSpecialRule.Stake).Accepted, "Ein negativer Einsatz angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = 12.5 }, CellSpecialRule.Stake).Accepted, "Eine Kommazahl angenommen.");
            Assert.IsFalse(Gib(bert, new { take = true }, CellSpecialRule.Stake).Accepted, "Eine fremde Nutzlast angenommen.");

            Assert.IsTrue(Gib(bert, new { amount = Feld }, CellSpecialRule.Stake).Accepted, "Genau die Grenze abgewiesen.");
            Assert.IsTrue(Gib(anna, new { amount = 400 }, CellSpecialRule.Stake).Accepted);
        }

        [TestMethod]
        public void ASecondStakeAndAForeignRuleAreRejected()
        {
            Starte(CellSpecialRule.Stake);

            Assert.IsTrue(Gib(anna, new { amount = 50 }, CellSpecialRule.Stake).Accepted);
            Assert.IsFalse(Gib(anna, new { amount = 60 }, CellSpecialRule.Stake).Accepted, "Zweite Abgabe angenommen.");
            Assert.AreEqual(50, Info<StakeClientInfo>(anna).OwnAmount, "Die erste Abgabe wurde ueberschrieben.");

            Assert.IsFalse(Gib(bert, new { doubled = true }, CellSpecialRule.Risk).Accepted, "Abgabe fuer eine fremde Regel angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = 10 }, CellSpecialRule.None).Accepted, "Minispiel-Abgabe in der Vorphase angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = 10 }, CellSpecialRule.Stake, zustand.Durchgang - 1).Accepted, "Alter Durchgang angenommen.");
        }

        [TestMethod]
        public void AnotherPlayerNeverSeesTheOwnStake()
        {
            Starte(CellSpecialRule.Stake);
            Gib(anna, new { amount = 77 }, CellSpecialRule.Stake);

            Assert.IsNull(Info<StakeClientInfo>(bert).OwnAmount, "Bert sieht Annas Einsatz.");
            Assert.IsTrue(((MinigameClientInfo)zustand.InfoFor(anna)).OwnSubmitted);
            Assert.AreEqual(CellSpecialRule.Stake, ((MinigameClientInfo)zustand.InfoFor(anna)).SpecialRule, "Das Telefon erfaehrt die Regel nicht.");
        }

        [TestMethod]
        public void ThePhaseClosesWhenEveryoneHasChosenAndKeepsTheChoices()
        {
            Starte(CellSpecialRule.Risk);

            Assert.IsFalse(Gib(anna, new { doubled = "ja" }, CellSpecialRule.Risk).Accepted, "Kaputte Wahl angenommen.");
            Assert.IsNull(Gib(anna, new { doubled = true }, CellSpecialRule.Risk).Closed);
            var zu = Gib(bert, new { doubled = false }, CellSpecialRule.Risk).Closed;

            Assert.IsNotNull(zu, "Nach der letzten Abgabe ist die Vorphase nicht zu.");
            Assert.IsTrue(zu.Finished);
            Assert.AreEqual(true, zu.Submissions[anna.Id]);
            Assert.AreEqual(false, zu.Submissions[bert.Id]);
            Assert.AreEqual(true, Info<RiskClientInfo>(anna).OwnDoubled, "Nach dem Schliessen ist die eigene Wahl weg.");
        }

        [TestMethod]
        public void TheGameMasterClosesWithoutTheMissingPhone()
        {
            Starte(CellSpecialRule.Stake);
            Gib(anna, new { amount = 20 }, CellSpecialRule.Stake);

            var zu = zustand.CloseRound();

            Assert.IsNotNull(zu);
            CollectionAssert.AreEquivalent(new[] { anna.Id }, zu.Submissions.Keys.ToList(), "Wer nichts abgab, steht in den Abgaben.");
            Assert.IsFalse(Gib(bert, new { amount = 20 }, CellSpecialRule.Stake).Accepted, "Nach dem Schliessen angenommen.");
        }
    }
}
