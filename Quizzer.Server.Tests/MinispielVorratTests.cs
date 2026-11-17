using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Vorrat;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Der Vorrat geteilter Minispiele im Servermodus (M35 C8, V4): Konto A legt ihn an, Konto B sieht ihn, zieht ihn in
    /// ein eigenes Spiel und kann ihn nicht ändern; legt B danach selbst an, entsteht nichts doppelt und nichts wird
    /// abgewiesen.
    /// <para>
    /// Die Ids sind fest und die Serverdatenbank teilen sich alle Tests - darum räumt jeder Test den Vorrat am Server
    /// vorher und nachher weg.
    /// </para>
    /// </summary>
    [TestClass]
    public sealed class MinispielVorratTests
    {
        private static readonly List<Guid> VorratIds = MinispielVorrat.Arten.Select(MinispielVorrat.IdFor).ToList();

        [TestInitialize]
        public Task SetUp() => RaeumeServerAsync();

        [TestCleanup]
        public Task TearDown() => RaeumeServerAsync();

        private static async Task RaeumeServerAsync()
        {
            await using var db = new DataContext(ServerTestHost.ConnectionString);

            await db.GameGridCoordinates
                .Where(c => c.QuestionBaseId != null && VorratIds.Contains(c.QuestionBaseId.Value))
                .ExecuteDeleteAsync();

            // TPT: ExecuteDelete geht ueber die Hierarchie nicht - nachverfolgt loeschen.
            db.QuestionBases.RemoveRange(await db.QuestionBases.Where(q => VorratIds.Contains(q.Id)).ToListAsync());
            db.Categories.RemoveRange(await db.Categories.Where(k => k.Id == MinispielVorrat.KategorieId).ToListAsync());
            await db.SaveChangesAsync();
        }

        private static async Task<(ClientHarness Client, Guid Me)> SignedInAsync(
            Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string name)
        {
            var (_, tokens) = await ServerTestHost.NewMemberAsync(factory, name);
            var client = await ClientHarness.CreateAsync(factory, name);
            await client.Api.LoginAsync(tokens.Account.UserName, "Mitglieds-Kennwort-123");
            await client.Sync.PullAsync();
            return (client, tokens.Account.PlayerId);
        }

        /// <summary>Wie der Knopf im Fragenkatalog: mit den Controllern des Programms, auf der lokalen Datenbank des Clients.</summary>
        private static async Task<MinispielVorrat.Ergebnis> AnlegenAsync(ClientHarness client, Guid besitzer)
        {
            using var fragen = new QuestionBasesController();
            fragen.Context.Dispose();
            fragen.Context = client.Local();

            return await MinispielVorrat.CreateAsync(fragen, besitzer);
        }

        private static string Abgewiesen(IEnumerable<SyncRejected> rejected)
            => string.Join("; ", rejected.Select(r => $"{r.Type} {r.Id} {r.Reason} {r.Message}"));

        [TestMethod]
        public async Task AStockSharedByAIsSeenUsedButNotChangedByB()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (a, aId) = await SignedInAsync(factory, "VorratA");
            var (b, bId) = await SignedInAsync(factory, "VorratB");
            await using var _ = a;
            await using var __ = b;

            Assert.AreEqual(VorratIds.Count, (await AnlegenAsync(a, aId)).Neu);
            var vonA = await a.Sync.PushAsync();
            Assert.AreEqual(0, vonA.Rejected.Count, Abgewiesen(vonA.Rejected));

            // B sieht ihn nach dem Abgleich: alle Arten, geteilt, Besitzer A.
            await b.Sync.PullAsync();
            await using (var lokal = b.Local())
            {
                var vorrat = await lokal.QuestionBases.OfType<MinigameQuestion>().Where(q => VorratIds.Contains(q.Id)).ToListAsync();
                Assert.AreEqual(VorratIds.Count, vorrat.Count, "B sieht den Vorrat nicht vollständig.");
                Assert.IsTrue(vorrat.All(q => q.IsShared && q.OwnerPlayerId == aId), "Der Vorrat kam nicht als geteilter Bestand von A an.");
            }

            // B legt danach selbst an: nichts doppelt, nichts abgewiesen.
            var vonB = await AnlegenAsync(b, bId);
            Assert.AreEqual(0, vonB.Neu, "B hat nach dem Abgleich noch etwas angelegt.");
            var bPush = await b.Sync.PushAsync();
            Assert.AreEqual(0, bPush.Rejected.Count, Abgewiesen(bPush.Rejected));
            await AmServerGenauEinmalVonAsync(aId);

            await ZiehtInEigenesSpielAsync(b, bId);

            // B ändert eine Vorratsfrage: der Server weist ab, der Stand bleibt.
            await using (var lokal = b.Local())
            {
                var frage = await lokal.QuestionBases.SingleAsync(q => q.Id == MinispielVorrat.IdFor(MinigameKind.Reaction));
                frage.Points = 999;
                await lokal.SaveChangesAsync();
            }

            var geaendert = await b.Sync.PushAsync();
            Assert.IsTrue(geaendert.Rejected.Any(r => r.Id == MinispielVorrat.IdFor(MinigameKind.Reaction) && r.Reason == SyncRejectReason.Forbidden),
                "Die Änderung an fremdem Vorrat wurde nicht abgewiesen: " + Abgewiesen(geaendert.Rejected));

            await using (var server = new DataContext(ServerTestHost.ConnectionString))
                Assert.AreEqual(MinispielVorrat.Punkte,
                    (await server.QuestionBases.SingleAsync(q => q.Id == MinispielVorrat.IdFor(MinigameKind.Reaction))).Points);
        }

        /// <summary>B legt eine Zelle seines eigenen Spiels auf eine Vorratsfrage; der Server nimmt sie an.</summary>
        private static async Task ZiehtInEigenesSpielAsync(ClientHarness b, Guid bId)
        {
            var spiel = SyncTestData.NewGame("Bs Abend");
            spiel.Game.ModeratorPlayerId = bId;
            spiel.Question.OwnerPlayerId = bId;
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Game.Id, QuestionBaseId = MinispielVorrat.IdFor(MinigameKind.Reaction), X = 1, Y = 0 };

            await using (var lokal = b.Local())
            {
                lokal.AddRange(spiel.Category, spiel.Question, spiel.Game, spiel.Cell, zelle);
                lokal.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = bId });
                await lokal.SaveChangesAsync();
            }

            var mitZelle = await b.Sync.PushAsync();
            Assert.AreEqual(0, mitZelle.Rejected.Count, Abgewiesen(mitZelle.Rejected));

            await using var server = new DataContext(ServerTestHost.ConnectionString);
            Assert.IsTrue(await server.GameGridCoordinates.AnyAsync(c => c.Id == zelle.Id), "Die Zelle auf den Vorrat kam nicht an.");
        }

        /// <summary>Am Server liegt jede Vorratsfrage genau einmal, geteilt, mit dem Besitzer, der zuerst angelegt hat.</summary>
        private static async Task AmServerGenauEinmalVonAsync(Guid besitzer)
        {
            await using var server = new DataContext(ServerTestHost.ConnectionString);

            var vorrat = await server.QuestionBases.Where(q => VorratIds.Contains(q.Id)).ToListAsync();
            Assert.AreEqual(VorratIds.Count, vorrat.Count, "Am Server liegt der Vorrat nicht genau einmal.");
            Assert.IsTrue(vorrat.All(q => q.IsShared && q.OwnerPlayerId == besitzer), "Am Server hat der Vorrat Besitzer oder Teilung verloren.");
            Assert.AreEqual(1, await server.Categories.CountAsync(k => k.Id == MinispielVorrat.KategorieId));
        }

        /// <summary>
        /// B und C haben abgeholt, <b>bevor</b> A seinen Vorrat schickt. B legt trotzdem an: gemessen weist der Server
        /// jede Vorratsfrage als Konflikt ab (Besitzer B statt A, also anderer Hash), die gleiche Kategorie geht durch,
        /// am Server entsteht nichts doppelt, und erst das Abholen räumt die Abweisungen. C geht den Weg des Knopfs im
        /// Servermodus - erst abholen, dann anlegen - und es entsteht nichts und nichts wird abgewiesen.
        /// </summary>
        [TestMethod]
        public async Task CreatingBeforeTheFirstPullConflictsAndPullingFirstAvoidsIt()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (a, aId) = await SignedInAsync(factory, "VorratA2");
            var (b, bId) = await SignedInAsync(factory, "VorratB2");
            var (c, cId) = await SignedInAsync(factory, "VorratC2");
            await using var _ = a;
            await using var __ = b;
            await using var ___ = c;

            await AnlegenAsync(a, aId);
            Assert.AreEqual(0, (await a.Sync.PushAsync()).Rejected.Count);

            Assert.AreEqual(VorratIds.Count, (await AnlegenAsync(b, bId)).Neu, "B kennt As Vorrat noch nicht - er legt alles an.");
            var bPush = await b.Sync.PushAsync();

            CollectionAssert.AreEquivalent(VorratIds, bPush.Rejected.Select(r => r.Id).ToList(), Abgewiesen(bPush.Rejected));
            Assert.IsTrue(bPush.Rejected.All(r => r.Reason == SyncRejectReason.Conflict), Abgewiesen(bPush.Rejected));
            Assert.AreEqual(VorratIds.Count, b.Sync.CountRejections(), "Die Abweisungen sind nicht sichtbar.");

            // V12: B hatte die Zeilen nie - "inzwischen geaendert" fuehrte in die Irre; es heisst "schon angelegt", Rat: abholen.
            Assert.IsTrue(bPush.Rejected.All(r => r.Message.Contains("schon angelegt") && r.Message.Contains("Abholen")), Abgewiesen(bPush.Rejected));
            Assert.IsTrue((await b.Sync.GetRejectionsAsync()).All(o => o.ErstAnlage), "B erkennt das Erstanlegen nicht an seinem Manifest.");
            await AmServerGenauEinmalVonAsync(aId);

            await b.Sync.PullAsync();
            Assert.AreEqual(0, b.Sync.CountRejections(), "Das Abholen hat die Abweisungen nicht geräumt.");
            await using (var lokal = b.Local())
                Assert.IsTrue(await lokal.QuestionBases.Where(q => VorratIds.Contains(q.Id)).AllAsync(q => q.OwnerPlayerId == aId),
                    "Nach dem Abholen gilt lokal nicht der Serverstand.");

            // Der Knopf im Servermodus: erst abholen, dann anlegen.
            await c.Sync.PullAsync();
            Assert.AreEqual(0, (await AnlegenAsync(c, cId)).Neu, "Nach dem Abholen hat C noch etwas angelegt.");
            var cPush = await c.Sync.PushAsync();
            Assert.AreEqual(0, cPush.Rejected.Count, Abgewiesen(cPush.Rejected));
            await AmServerGenauEinmalVonAsync(aId);
        }
    }
}
