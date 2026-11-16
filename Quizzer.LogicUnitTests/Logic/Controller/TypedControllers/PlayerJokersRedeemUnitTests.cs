using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Einlösen und Freigeben am Controller (M35 C7b) - der zweite Riegel hinter der Joker-Runde am Server. Jeder
    /// Missbrauchsfall geht hier ohne die Runde durch, also ohne den ersten Riegel.
    /// </summary>
    [TestClass]
    public class PlayerJokersRedeemUnitTests
    {
        private static readonly DateTimeOffset Jetzt = new(2026, 11, 16, 20, 0, 0, TimeSpan.FromHours(1));

        private Game spiel = null!;
        private Game anderes = null!;
        private Player anna = null!;
        private Player ben = null!;
        private Player clara = null!;
        private GameGridCoordinate zelle = null!;
        private GameGridCoordinate fremdeZelle = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            anna = Spieler("Anna");
            ben = Spieler("Ben");
            clara = Spieler("Clara");
            spiel = new Game { Id = Guid.NewGuid(), Designation = "Jokerspiel", Phase = 1, SuggestedPhases = 1 };
            anderes = new Game { Id = Guid.NewGuid(), Designation = "Anderes Spiel", Phase = 1, SuggestedPhases = 1 };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1 };
            fremdeZelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = anderes.Id, X = 0, Y = 0, Phase = 1 };

            using var ctrlSpieler = new PlayersController();
            using var ctrlSpiel = new GamesController(ctrlSpieler);
            using var ctrlZelle = new GameGridCoordinatesController(ctrlSpieler);
            using var ctrlLink = new PlayerXGamesController(ctrlSpieler);

            foreach (var p in new[] { anna, ben, clara })
                await ctrlSpieler.InsertAsync(p);

            await ctrlSpiel.InsertAsync(spiel);
            await ctrlSpiel.InsertAsync(anderes);
            await ctrlZelle.InsertAsync(zelle);
            await ctrlZelle.InsertAsync(fremdeZelle);

            // Clara spielt nur im anderen Spiel mit.
            foreach (var (s, p) in new[] { (spiel, anna), (spiel, ben), (anderes, clara), (anderes, anna) })
                await ctrlLink.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = s.Id, PlayerId = p.Id });

            await ctrlSpieler.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                foreach (var id in new[] { spiel.Id, anderes.Id })
                {
                    if (await ctrl.GetAsync(id) != null)
                        await ctrl.DeleteAsync(id);
                }

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                foreach (var id in new[] { anna.Id, ben.Id, clara.Id })
                {
                    if (await ctrl.GetAsync(id) != null)
                        await ctrl.DeleteAsync(id);
                }

                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        private static Player Spieler(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        private async Task<PlayerJoker> VergebenAsync(Player p, JokerKind art, Game? imSpiel = null)
        {
            using var ctrl = new PlayerJokersController();
            var joker = await ctrl.GrantAsync((imSpiel ?? spiel).Id, p.Id, art, "verdient", Jetzt.AddHours(-1));
            await ctrl.SaveChangesAsync();
            return joker;
        }

        /// <summary>Löst ein, schreibt und liest den Joker frisch aus der Datenbank.</summary>
        private async Task<(JokerRedeemOutcome Ergebnis, PlayerJoker Danach)> EinloesenAsync(
            PlayerJoker joker, Player wer, Guid? ziel = null, Guid? zellId = null, Guid? spielId = null)
        {
            JokerRedeemOutcome ergebnis;

            using (var ctrl = new PlayerJokersController())
            {
                ergebnis = await ctrl.RedeemAsync(new JokerRedemption(joker.Id, wer.Id, spielId ?? spiel.Id, zellId ?? zelle.Id, ziel), Jetzt);
                await ctrl.SaveChangesAsync();
            }

            using var lesen = new PlayerJokersController();
            return (ergebnis, (await lesen.GetAsync(joker.Id))!);
        }

        private static void Offen(PlayerJoker joker, string warum)
            => Assert.AreEqual((null, null, null), (joker.GameGridCoordinateId, joker.RedeemedAt, joker.TargetPlayerId), warum);

        [TestMethod]
        public async Task TheOwnOpenJokerIsRedeemedWithCellTimeAndTarget()
        {
            var sperre = await VergebenAsync(anna, JokerKind.Block);

            var (ergebnis, danach) = await EinloesenAsync(sperre, anna, ziel: ben.Id);

            Assert.AreEqual(JokerRedeemOutcome.Redeemed, ergebnis);
            Assert.AreEqual((zelle.Id, Jetzt, ben.Id), (danach.GameGridCoordinateId, danach.RedeemedAt, danach.TargetPlayerId));
            Assert.IsTrue(danach.IsRedeemed);
        }

        [TestMethod]
        public async Task AForeignJokerIsRejectedAndStaysOpen()
        {
            var bens = await VergebenAsync(ben, JokerKind.Double);

            var (ergebnis, danach) = await EinloesenAsync(bens, anna);

            Assert.AreEqual(JokerRedeemOutcome.NotOwner, ergebnis);
            Offen(danach, "Anna hat Bens Joker eingelöst.");
        }

        [TestMethod]
        public async Task AnUnknownOrAlreadyRedeemedJokerIsRejected()
        {
            var unbekannt = new PlayerJoker { Id = Guid.NewGuid() };
            using (var ctrl = new PlayerJokersController())
                Assert.AreEqual(JokerRedeemOutcome.NotFound,
                    await ctrl.RedeemAsync(new JokerRedemption(unbekannt.Id, anna.Id, spiel.Id, zelle.Id, null), Jetzt));

            var doppelt = await VergebenAsync(anna, JokerKind.Double);
            var zweiteZelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 1, Y = 0, Phase = 1 };
            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(zweiteZelle);
                await ctrl.SaveChangesAsync();
            }

            Assert.AreEqual(JokerRedeemOutcome.Redeemed, (await EinloesenAsync(doppelt, anna)).Ergebnis);

            var (ergebnis, danach) = await EinloesenAsync(doppelt, anna, zellId: zweiteZelle.Id);

            Assert.AreEqual(JokerRedeemOutcome.AlreadyRedeemed, ergebnis);
            Assert.AreEqual(zelle.Id, danach.GameGridCoordinateId, "Der eingelöste Joker ist auf eine zweite Zelle gewandert.");
        }

        [TestMethod]
        public async Task ASecondJokerForTheSameCellIsRejected()
        {
            var erster = await VergebenAsync(anna, JokerKind.Double);
            var zweiter = await VergebenAsync(anna, JokerKind.Shield);

            Assert.AreEqual(JokerRedeemOutcome.Redeemed, (await EinloesenAsync(erster, anna)).Ergebnis);

            var (ergebnis, danach) = await EinloesenAsync(zweiter, anna);

            Assert.AreEqual(JokerRedeemOutcome.SecondJokerForCell, ergebnis);
            Offen(danach, "Ein zweiter Joker derselben Frage wurde eingelöst.");
        }

        [TestMethod]
        public async Task ABlockWithoutTargetOnSelfOrOnANonPlayerIsRejected()
        {
            var sperre = await VergebenAsync(anna, JokerKind.Block);

            foreach (var (ziel, fall) in new (Guid?, string)[] { (null, "ohne Ziel"), (anna.Id, "gegen sich selbst"), (clara.Id, "gegen eine Nicht-Mitspielerin") })
            {
                var (ergebnis, danach) = await EinloesenAsync(sperre, anna, ziel: ziel);

                Assert.AreEqual(JokerRedeemOutcome.TargetInvalid, ergebnis, $"Sperre {fall} angenommen.");
                Offen(danach, $"Sperre {fall} wurde geschrieben.");
            }
        }

        [TestMethod]
        public async Task ATargetOnDoubleOrShieldIsRejected()
        {
            var doppelt = await VergebenAsync(anna, JokerKind.Double);

            var (ergebnis, danach) = await EinloesenAsync(doppelt, anna, ziel: ben.Id);

            Assert.AreEqual(JokerRedeemOutcome.TargetInvalid, ergebnis);
            Offen(danach, "Ein Doppelt mit Ziel wurde geschrieben.");
        }

        [TestMethod]
        public async Task AJokerOfAnotherGameOrACellOfAnotherGameIsRejected()
        {
            var imAnderen = await VergebenAsync(anna, JokerKind.Double, anderes);
            var hier = await VergebenAsync(anna, JokerKind.Shield);

            Assert.AreEqual(JokerRedeemOutcome.WrongGame, (await EinloesenAsync(imAnderen, anna)).Ergebnis);

            var (ergebnis, danach) = await EinloesenAsync(hier, anna, zellId: fremdeZelle.Id);

            Assert.AreEqual(JokerRedeemOutcome.CellNotInGame, ergebnis);
            Offen(danach, "Der Joker hängt an einer Zelle eines anderen Spiels.");
        }

        [TestMethod]
        public async Task ReleaseReopensOnlyTheNamedJokersOfThatCell()
        {
            var annas = await VergebenAsync(anna, JokerKind.Block);
            var bens = await VergebenAsync(ben, JokerKind.Double);
            await EinloesenAsync(annas, anna, ziel: ben.Id);
            await EinloesenAsync(bens, ben);

            using (var ctrl = new PlayerJokersController())
            {
                Assert.AreEqual(0, await ctrl.ReleaseAsync(fremdeZelle.Id, [annas.Id]), "Freigegeben an einer Zelle, an der er nicht hängt.");
                Assert.AreEqual(1, await ctrl.ReleaseAsync(zelle.Id, [annas.Id]));
                await ctrl.SaveChangesAsync();
            }

            using var lesen = new PlayerJokersController();
            Offen((await lesen.GetAsync(annas.Id))!, "Annas Joker ist nach dem Freigeben nicht offen.");
            Assert.IsTrue((await lesen.GetAsync(bens.Id))!.IsRedeemed, "Freigegeben wurde ein Joker, der nicht genannt war.");
        }
    }
}
