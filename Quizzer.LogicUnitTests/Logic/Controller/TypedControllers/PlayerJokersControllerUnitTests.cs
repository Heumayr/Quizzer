using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Joker vergeben, zurücknehmen und beim Löschen eines Mitspielers aufräumen (M35 C7a).
    /// </summary>
    [TestClass]
    public class PlayerJokersControllerUnitTests
    {
        private Game spiel = null!;
        private Player anna = null!;
        private Player ben = null!;
        private GameGridCoordinate zelle = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            ben = new Player { Id = Guid.NewGuid(), Designation = "Ben", DisplayName = "Ben" };
            spiel = new Game { Id = Guid.NewGuid(), Designation = "Jokerspiel", Phase = 1, SuggestedPhases = 1 };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1 };

            using var ctrlSpieler = new PlayersController();
            using var ctrlSpiel = new GamesController(ctrlSpieler);
            using var ctrlZelle = new GameGridCoordinatesController(ctrlSpieler);

            await ctrlSpieler.InsertAsync(anna);
            await ctrlSpieler.InsertAsync(ben);
            await ctrlSpiel.InsertAsync(spiel);
            await ctrlZelle.InsertAsync(zelle);
            await ctrlSpieler.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                if (await ctrl.GetAsync(spiel.Id) != null)
                {
                    await ctrl.DeleteAsync(spiel.Id);
                    await ctrl.SaveChangesAsync();
                }
            }

            using (var ctrl = new PlayersController())
            {
                foreach (var id in new[] { anna.Id, ben.Id })
                {
                    if (await ctrl.GetAsync(id) != null)
                        await ctrl.DeleteAsync(id);
                }

                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        private async Task<PlayerJoker> EingeloestAsync(Guid spieler, Guid? ziel = null)
        {
            var joker = new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler, Kind = ziel == null ? JokerKind.Double : JokerKind.Block,
                Reason = "eingelöst", GrantedAt = DateTimeOffset.Now, GameGridCoordinateId = zelle.Id,
                RedeemedAt = DateTimeOffset.Now, TargetPlayerId = ziel,
            };

            using var ctrl = new PlayerJokersController();
            await ctrl.InsertAsync(joker);
            await ctrl.SaveChangesAsync();
            return joker;
        }

        [TestMethod]
        public async Task GrantingWritesWhoWhatWhyAndWhen()
        {
            var jetzt = new DateTimeOffset(2026, 11, 16, 20, 15, 0, TimeSpan.FromHours(1));

            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.GrantAsync(spiel.Id, anna.Id, JokerKind.Shield, "  Minispiel gewonnen  ", jetzt);
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new PlayerJokersController();
            var joker = (await pruefer.GetForGameAsync(spiel.Id)).Single();

            Assert.AreEqual((anna.Id, JokerKind.Shield, "Minispiel gewonnen", jetzt), (joker.PlayerId, joker.Kind, joker.Reason, joker.GrantedAt));
            Assert.IsFalse(joker.IsRedeemed, "Ein frisch vergebener Joker ist schon eingelöst.");
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(PlayerJoker.MaxReasonLength + 1)]
        public async Task AMissingOrTooLongReasonIsRefused(int laenge)
        {
            using var ctrl = new PlayerJokersController();

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => ctrl.GrantAsync(spiel.Id, anna.Id, JokerKind.Double, new string('x', laenge), DateTimeOffset.Now));
        }

        [TestMethod]
        public async Task AReasonRightAtTheLimitIsAccepted()
        {
            using (var ctrl = new PlayerJokersController())
            {
                await ctrl.GrantAsync(spiel.Id, anna.Id, JokerKind.Double, new string('x', PlayerJoker.MaxReasonLength), DateTimeOffset.Now);
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new PlayerJokersController();
            Assert.AreEqual(1, (await pruefer.GetForGameAsync(spiel.Id)).Count);
        }

        /// <summary>Die Spalte folgt der Konstante, die Server und Maske prüfen.</summary>
        [TestMethod]
        public void TheColumnHasTheSameLimitAsTheChecks()
        {
            using var context = new DataContext();
            var spalte = context.Model.FindEntityType(typeof(PlayerJoker))!.FindProperty(nameof(PlayerJoker.Reason))!;

            Assert.AreEqual(PlayerJoker.MaxReasonLength, spalte.GetMaxLength());
        }

        [TestMethod]
        public async Task AnOpenJokerCanBeWithdrawn()
        {
            Guid id;
            using (var ctrl = new PlayerJokersController())
            {
                id = (await ctrl.GrantAsync(spiel.Id, anna.Id, JokerKind.Double, "zu früh", DateTimeOffset.Now)).Id;
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayerJokersController())
            {
                Assert.IsTrue(await ctrl.WithdrawAsync(id));
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new PlayerJokersController();
            Assert.AreEqual(0, (await pruefer.GetForGameAsync(spiel.Id)).Count, "Der zurückgenommene Joker steht noch da.");
        }

        [TestMethod]
        public async Task ARedeemedJokerCannotBeWithdrawn()
        {
            var joker = await EingeloestAsync(anna.Id);

            using (var ctrl = new PlayerJokersController())
            {
                Assert.IsFalse(await ctrl.WithdrawAsync(joker.Id));
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new PlayerJokersController();
            Assert.AreEqual(1, (await pruefer.GetForGameAsync(spiel.Id)).Count, "Der eingelöste Joker ist weg.");
        }

        /// <summary>Eingelöst heißt auch: nur die Zeit steht, die Zelle (noch) nicht.</summary>
        [TestMethod]
        public void EitherRedemptionFieldMakesItRedeemed()
        {
            Assert.IsTrue(new PlayerJoker { RedeemedAt = DateTimeOffset.Now }.IsRedeemed);
            Assert.IsTrue(new PlayerJoker { GameGridCoordinateId = Guid.NewGuid() }.IsRedeemed);
            Assert.IsFalse(new PlayerJoker().IsRedeemed);
        }

        /// <summary>
        /// Wer gesperrt wurde, lässt sich trotzdem löschen - die Sperre geht mit ihm (NO ACTION am Ziel),
        /// seine eigenen Joker nimmt die Kaskade. Die Sperre des anderen gegen jemand Drittes bleibt.
        /// </summary>
        [TestMethod]
        public async Task APlayerHitByABlockCanBeDeletedAndTheBlockGoesWithHim()
        {
            var gegenBen = await EingeloestAsync(anna.Id, ziel: ben.Id);
            var vonBen = await EingeloestAsync(ben.Id);

            using (var ctrl = new PlayersController())
            {
                await ctrl.DeleteAsync(ben.Id);
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new PlayerJokersController();
            var uebrig = (await pruefer.GetForGameAsync(spiel.Id)).Select(j => j.Id).ToList();

            CollectionAssert.DoesNotContain(uebrig, gegenBen.Id, "Die Sperre gegen den gelöschten Mitspieler steht noch da.");
            CollectionAssert.DoesNotContain(uebrig, vonBen.Id, "Der Joker des gelöschten Mitspielers steht noch da.");
        }

        /// <summary>
        /// V16 (M37 D3), wie V05 beim Spiel: scheitert das Speichern nach dem Aufräumen, steht die Sperre gegen ihn noch.
        /// Die Störung ist eine Zelle zu einem Spiel, das es nicht gibt - ihr Fremdschlüssel scheitert im selben Speichern.
        /// </summary>
        [TestMethod]
        public async Task AFailedPlayerDeleteLeavesTheBlockAgainstHimInPlace()
        {
            var gegenBen = await EingeloestAsync(anna.Id, ziel: ben.Id);

            using (var ctrl = new PlayersController())
            using (var stoerung = new GameGridCoordinatesController(ctrl))
            {
                await ctrl.DeleteAsync(ben.Id);
                await stoerung.InsertAsync(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Phase = 1 });

                await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    async () => await ctrl.SaveChangesAsync(),
                    "Die Störung hätte das Speichern scheitern lassen müssen - sonst misst dieser Test nichts.");
            }

            TestDatabase.ClearDiscardedChanges();

            using var spieler = new PlayersController();
            Assert.IsNotNull(await spieler.GetAsync(ben.Id), "Ben ist weg.");

            using var pruefer = new PlayerJokersController();
            CollectionAssert.Contains((await pruefer.GetForGameAsync(spiel.Id)).Select(j => j.Id).ToList(), gegenBen.Id,
                "Die Sperre gegen Ben ist weg, Ben steht noch.");
        }

        [TestMethod]
        public void JokersCameWithFormat23()
        {
            var format = SyncSnapshot.CurrentFormatVersion;
            Assert.IsTrue(format >= 23,
                "Neuer Zeilentyp PlayerJoker ohne neues Abgleichformat - ein altes WPF kennt ihn nicht.");
        }

        /// <summary>Je Joker-Art das Format, mit dem sie kam - eine neue Art ohne Eintrag wird hier rot.</summary>
        [TestMethod]
        public void EveryJokerKindHasAFormatThatIsNotAhead()
        {
            var seitFormat = new Dictionary<JokerKind, int>
            {
                [JokerKind.Double] = 23,
                [JokerKind.Shield] = 23,
                [JokerKind.Block] = 23,
            };

            var ohne = Enum.GetValues<JokerKind>().Where(k => !seitFormat.ContainsKey(k)).ToList();

            Assert.AreEqual(0, ohne.Count, "Neue Joker-Art ohne Abgleichformat: " + string.Join(", ", ohne));
            Assert.IsTrue(seitFormat.Values.Max() <= SyncSnapshot.CurrentFormatVersion);
        }
    }
}
