using Quizzer.Client;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// V06 (M37 D3): die Massenlösch-Sperre zählt nur Fragen und Spiele, und wer bewusst so viel löscht, gibt genau
    /// diese Löschung frei. Gemessen am 2026-11-16: mit 50 fremden Mitspielern und Kategorien gingen 2 Spiele und
    /// 8 Fragen durch.
    /// </summary>
    public sealed partial class ClientSafetyTests
    {
        private static async Task<List<Guid>> OwnQuestionsAsync(ClientHarness client, Guid me, int anzahl, string praefix)
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = praefix + "-Kategorie" };
            var ids = new List<Guid>();

            await using (var local = client.Local())
            {
                local.Categories.Add(kategorie);
                for (var i = 0; i < anzahl; i++)
                {
                    var frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = praefix + i, CategoryId = kategorie.Id, OwnerPlayerId = me };
                    local.DefaultQuestions.Add(frage);
                    ids.Add(frage.Id);
                }
                await local.SaveChangesAsync();
            }

            var angelegt = await client.Sync.PushAsync();
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));
            return ids;
        }

        private static async Task DeleteQuestionsLocallyAsync(ClientHarness client, IReadOnlyCollection<Guid> ids)
        {
            await using var local = client.Local();
            local.QuestionBases.RemoveRange(local.QuestionBases.Where(q => ids.Contains(q.Id)));
            await local.SaveChangesAsync();
        }

        private static async Task<int> OnServerAsync(ClientHarness client, IReadOnlyCollection<Guid> ids)
            => (await client.Api.GetSnapshotAsync()).Rows.Count(r => ids.Contains(r.Id));

        /// <summary>
        /// Ein fremdes Konto legt 50 Mitspieler und 50 Kategorien an; A verliert lokal seine 2 Spiele mit 8 Fragen. Vor V06
        /// ging der Push durch, und am Server waren 0 von 2 und 0 von 8 übrig.
        /// </summary>
        [TestMethod]
        public async Task ForeignPlayersAndCategoriesDoNotDiluteTheGuard()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (fremd, _) = await ServerTestHost.NewMemberAsync(factory, "Fremd");
            var (client, me) = await SignedInAsync(factory, "Duenn");
            await using var _ = client;

            var masse = new SyncPushRequest();
            for (var i = 0; i < 50; i++)
            {
                masse.Upserts.Add(SyncTestData.New(new Player { Id = Guid.NewGuid(), Designation = "Gast" + i, DisplayName = "Gast" + i }));
                masse.Upserts.Add(SyncTestData.New(new Category { Id = Guid.NewGuid(), Designation = "Fremd" + i }));
            }
            var angelegt = await (await fremd.PostAsJsonAsync("/api/v1/sync/push", masse)).Content.ReadFromJsonAsync<SyncPushResponse>();
            Assert.AreEqual(100, angelegt!.Accepted.Count, "Die fremden Zeilen kamen nicht an - sonst misst dieser Test nichts.");

            var fragen = await OwnQuestionsAsync(client, me, 8, "Spielfrage");
            var spiele = new List<Guid>();
            await using (var local = client.Local())
            {
                for (var s = 0; s < 2; s++)
                {
                    var spiel = new Game { Id = Guid.NewGuid(), Designation = "Spiel" + s, ModeratorPlayerId = me };
                    local.Games.Add(spiel);
                    spiele.Add(spiel.Id);
                    for (var i = 0; i < 4; i++)
                        local.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = fragen[s * 4 + i], X = i });
                }
                await local.SaveChangesAsync();
            }
            await client.Sync.PullAsync();

            await using (var local = client.Local())
            {
                local.GameGridCoordinates.RemoveRange(local.GameGridCoordinates.Where(c => spiele.Contains(c.GameId)));
                local.Games.RemoveRange(local.Games.Where(g => spiele.Contains(g.Id)));
                local.QuestionBases.RemoveRange(local.QuestionBases.Where(q => fragen.Contains(q.Id)));
                await local.SaveChangesAsync();
            }

            var gesperrt = await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());

            Assert.AreEqual(2, await OnServerAsync(client, spiele), "Spiele sind am Server gelöscht.");
            Assert.AreEqual(8, await OnServerAsync(client, fragen), "Fragen sind am Server gelöscht.");
            Assert.AreEqual(8, gesperrt.MassDeletion.Questions);
            Assert.AreEqual(2, gesperrt.MassDeletion.Games);
        }

        /// <summary>Bewusst gelöscht: ohne Freigabe hält die Sperre, mit Freigabe geht genau diese Löschung durch.</summary>
        [TestMethod]
        public async Task ADeliberateMassDeletionGoesThroughOnceApproved()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Bewusst");
            await using var _ = client;

            var fragen = await OwnQuestionsAsync(client, me, 12, "Aufraeumen");
            await DeleteQuestionsLocallyAsync(client, fragen);

            var gesperrt = await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());
            Assert.AreEqual(12, await OnServerAsync(client, fragen), "Ohne Freigabe wurde gelöscht.");
            Assert.AreEqual("12 Fragen", gesperrt.MassDeletion.Description);
            StringAssert.Contains(gesperrt.Message, "12 Fragen");

            var result = await client.Sync.PushApprovedAsync(gesperrt.MassDeletion);

            Assert.AreEqual(12, result.Deleted, "Die freigegebene Löschung ging nicht durch.");
            Assert.AreEqual(0, await OnServerAsync(client, fragen), "Die Fragen stehen am Server noch.");
            Assert.IsFalse((await client.Sync.PushAsync()).HadChanges, "Danach hängt noch etwas.");
        }

        /// <summary>Kommt nach der Rückfrage eine weitere Löschung dazu, deckt die Freigabe sie nicht - es bleibt alles stehen.</summary>
        [TestMethod]
        public async Task AnApprovalCoversExactlyTheNamedDeletion()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Genau");
            await using var _ = client;

            var fragen = await OwnQuestionsAsync(client, me, 13, "Genau");
            await DeleteQuestionsLocallyAsync(client, fragen.Take(12).ToList());
            var gefragt = await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());

            await DeleteQuestionsLocallyAsync(client, [fragen[12]]);

            await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushApprovedAsync(gefragt.MassDeletion));
            Assert.AreEqual(13, await OnServerAsync(client, fragen), "Die Freigabe für 12 Fragen hat mehr gelöscht.");
        }

        /// <summary>
        /// Der Hintergrund-Abgleich meldet die Sperre mit Zahlen, gibt sie aber nie frei - auch nicht beim zweiten Mal.
        /// </summary>
        [TestMethod]
        public async Task TheBackgroundSyncNeverLiftsTheGuard()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Hintergrund");
            await using var _ = client;

            var fragen = await OwnQuestionsAsync(client, me, 12, "Nebenher");
            await DeleteQuestionsLocallyAsync(client, fragen);

            using var background = new BackgroundSync(client.Sync, TimeSpan.FromMilliseconds(50));
            await background.FlushAsync();
            await background.FlushAsync();

            Assert.AreEqual(12, await OnServerAsync(client, fragen), "Der Hintergrund hat gelöscht.");
            Assert.AreEqual(SyncStatus.Rejected, background.Status.Status);
            Assert.AreEqual(12, background.Status.PendingMassDeletion?.Questions, "Die Statuszeile nennt nicht, was gelöscht würde.");
        }

        /// <summary>
        /// „Vom Server neu laden" und die Anmeldung schicken vorher hinaus und liefen bis D3 in dieselbe Sperre - ohne
        /// Ausweg. Mit Freigabe wird gelöscht und geladen.
        /// </summary>
        [TestMethod]
        public async Task ReloadingWithApprovalDeletesAndLoads()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Neuladen");
            await using var _ = client;

            var fragen = await OwnQuestionsAsync(client, me, 12, "Laden");
            await DeleteQuestionsLocallyAsync(client, fragen);

            var gesperrt = await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PullAsync());
            await client.Sync.PullApprovedAsync(gesperrt.MassDeletion);

            Assert.AreEqual(0, await OnServerAsync(client, fragen), "Die Fragen stehen am Server noch.");
            await using var local = client.Local();
            Assert.IsFalse(local.QuestionBases.Any(q => fragen.Contains(q.Id)), "Das Laden hat die gelöschten Fragen zurückgebracht.");
        }
    }
}
