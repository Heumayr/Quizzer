using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Base.States.Sonderregeln;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Sonderregeln;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Vorphasen der Regeln aus M35 C3b am Server, ohne Netz: Grenzen, Fremd- und Doppelabgabe, und dass
    /// keine Abgabe vor der Aufloesung bei einem anderen Telefon ankommt.
    /// </summary>
    [TestClass]
    public partial class SonderregelVorphaseC3bUnitTests
    {
        private const int Feld = 100;

        private Player anna = null!;
        private Player bert = null!;
        private Player clara = null!;
        private MinigameState zustand = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            clara = Spieler("Clara");

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert, clara })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            zustand = new MinigameState(new GameAccessor { GetGame = () => game });
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        /// <summary>Anna 400, Bert 30, Clara 0 auf dem Konto; Anna hat das Feld aufgedeckt.</summary>
        private void Starte(CellSpecialRule regel, Guid? ausloeser = null, int anteil = 0)
        {
            zustand.Configure(new MinigameSetup(default, "Sonderregel", "Regel", 1, CountdownSeconds: 0, SpecialRule: regel,
                TriggerPlayerId: ausloeser, FieldPoints: Feld, Share: anteil,
                Balances: new Dictionary<Guid, int> { [anna.Id] = 400, [bert.Id] = 30, [clara.Id] = 0 }));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Vorphase liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast, CellSpecialRule regel)
            => zustand.Submit(p, new MinigameAction
            {
                SpecialRule = regel,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private T Info<T>(Player p) => (T)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        // --- Auktion ---

        [TestMethod]
        public void TheBidIsCheckedAgainstEachPlayersOwnLimitAndZeroPasses()
        {
            Starte(CellSpecialRule.Auction);

            Assert.AreEqual(Feld, Info<AuctionClientInfo>(bert).Max, "Berts Grenze sind nicht die Feldpunkte.");
            Assert.AreEqual(400, Info<AuctionClientInfo>(anna).Max, "Annas Grenze ist nicht ihr Kontostand.");

            Assert.IsFalse(Gib(bert, new { amount = Feld + 1 }, CellSpecialRule.Auction).Accepted, "Ueber der Grenze angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = -1 }, CellSpecialRule.Auction).Accepted, "Ein negatives Gebot angenommen.");
            Assert.IsFalse(Gib(bert, new { doubled = true }, CellSpecialRule.Auction).Accepted, "Eine fremde Nutzlast angenommen.");
            Assert.IsFalse(Gib(bert, new { amount = 10 }, CellSpecialRule.Stake).Accepted, "Abgabe fuer eine fremde Regel angenommen.");

            Assert.IsTrue(Gib(clara, new { amount = 0 }, CellSpecialRule.Auction).Accepted, "Passen (0) abgewiesen.");
            Assert.IsTrue(Gib(bert, new { amount = Feld }, CellSpecialRule.Auction).Accepted, "Genau die Grenze abgewiesen.");
            Assert.IsFalse(Gib(bert, new { amount = 5 }, CellSpecialRule.Auction).Accepted, "Zweites Gebot angenommen.");
        }

        [TestMethod]
        public void BidsCarryTheirArrivalAndStaySecretUntilTheEnd()
        {
            Starte(CellSpecialRule.Auction);

            Gib(bert, new { amount = 60 }, CellSpecialRule.Auction);
            Gib(anna, new { amount = 60 }, CellSpecialRule.Auction);

            Assert.IsNull(Info<AuctionClientInfo>(clara).OwnAmount, "Clara sieht ein fremdes Gebot.");
            Assert.AreEqual(60, Info<AuctionClientInfo>(anna).OwnAmount, "Anna sieht ihr eigenes Gebot nicht.");
            Assert.IsNull(((AuctionClientInfo)zustand.Rules!.InfoFor(null, MinigamePhase.Running)!).OwnAmount,
                "Der Blick von aussen (Beamer) zeigt ein Gebot.");

            var zu = Gib(clara, new { amount = 0 }, CellSpecialRule.Auction).Closed;
            Assert.IsNotNull(zu, "Nach dem letzten Gebot ist die Vorphase nicht zu.");

            var gebote = zu.Submissions.ToDictionary(e => e.Key, e => (Gebot)e.Value);
            Assert.IsTrue(gebote[bert.Id].Eingang < gebote[anna.Id].Eingang, "Der Eingang zeigt die Reihenfolge nicht.");
            Assert.AreEqual(bert.Id, AuktionRegel.Ermittle(gebote, zweitpreis: false)!.Gewinner, "Bei Gleichstand gewinnt nicht das fruehere Gebot.");
        }
    }
}
