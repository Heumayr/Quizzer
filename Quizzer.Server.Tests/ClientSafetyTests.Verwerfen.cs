using Quizzer.Client;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// V17 (M37 D5, umkehrbare Annahme F-M37-15): nach „Nein" bei der Massenlösch-Sperre die aufgehaltenen Löschungen
    /// verwerfen und den Serverstand holen. Bis D5 hing danach jeder Abgleich, und das Gelöschte kam nie zurück, weil
    /// <see cref="ClientSyncService.PullAsync"/> zuerst hinausschickt.
    /// </summary>
    public sealed partial class ClientSafetyTests
    {
        private sealed record Bestand(Guid Kategorie, Guid Spiel, List<Guid> Fragen, List<Guid> Schritte, List<Guid> Zellen);

        /// <summary>Eine Kategorie mit Fragen, je mit einem Schritt, dazu ein Spiel mit vier Zellen auf den ersten vier.</summary>
        private static async Task<Bestand> BestandAsync(ClientHarness client, Guid me, int anzahl, string praefix)
        {
            var bestand = new Bestand(Guid.NewGuid(), Guid.NewGuid(), [], [], []);

            await using (var local = client.Local())
            {
                local.Categories.Add(new Category { Id = bestand.Kategorie, Designation = praefix + "-Kategorie" });
                for (var i = 0; i < anzahl; i++)
                {
                    var frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = praefix + i, CategoryId = bestand.Kategorie, OwnerPlayerId = me };
                    var schritt = new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = frage.Id, StepText = "Hinweis " + i };
                    local.DefaultQuestions.Add(frage);
                    local.QuestionStepResources.Add(schritt);
                    bestand.Fragen.Add(frage.Id);
                    bestand.Schritte.Add(schritt.Id);
                }

                local.Games.Add(new Game { Id = bestand.Spiel, Designation = praefix + "-Spiel", ModeratorPlayerId = me });
                for (var i = 0; i < 4; i++)
                {
                    var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = bestand.Spiel, QuestionBaseId = bestand.Fragen[i], X = i };
                    local.GameGridCoordinates.Add(zelle);
                    bestand.Zellen.Add(zelle.Id);
                }

                await local.SaveChangesAsync();
            }

            var angelegt = await client.Sync.PushAsync();
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));
            return bestand;
        }

        /// <summary>Spiel, Zellen, Fragen, Schritte und Kategorie lokal weg - so, wie es ein versehentliches Löschen hinterlässt.</summary>
        private static async Task DeleteAllLocallyAsync(ClientHarness client, Bestand b)
        {
            await using var local = client.Local();
            local.GameGridCoordinates.RemoveRange(local.GameGridCoordinates.Where(c => c.GameId == b.Spiel));
            local.Games.RemoveRange(local.Games.Where(g => g.Id == b.Spiel));
            local.QuestionStepResources.RemoveRange(local.QuestionStepResources.Where(s => b.Fragen.Contains(s.QuestionBaseId)));
            local.QuestionBases.RemoveRange(local.QuestionBases.Where(q => b.Fragen.Contains(q.Id)));
            local.Categories.RemoveRange(local.Categories.Where(c => c.Id == b.Kategorie));
            await local.SaveChangesAsync();
        }

        private static async Task<Dictionary<SyncKey, string>> ServerStandAsync(ClientHarness client)
            => (await client.Api.GetSnapshotAsync()).Rows.ToDictionary(r => r.Key, r => r.Hash);

        private static void AssertServerUnchanged(Dictionary<SyncKey, string> vorher, Dictionary<SyncKey, string> nachher)
        {
            var anders = vorher.Where(v => !nachher.TryGetValue(v.Key, out var h) || h != v.Value).Select(v => v.Key.Type + " " + v.Key.Id)
                .Concat(nachher.Keys.Where(k => !vorher.ContainsKey(k)).Select(k => "neu: " + k.Type + " " + k.Id))
                .ToList();
            Assert.AreEqual(0, anders.Count, "Am Server hat sich etwas geändert: " + string.Join("; ", anders.Take(5)));
        }

        /// <summary>
        /// Versehentlich gelöscht, bei der Rückfrage „Nein": verworfen kommen Fragen, Spiel, Schritte, Zellen und Kategorie
        /// lokal zurück, am Server ändert sich keine Zeile, und danach hängt nichts mehr.
        /// </summary>
        [TestMethod]
        public async Task DiscardingBringsTheRowsBackAndLeavesTheServerAlone()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Verwerfen");
            await using var _ = client;

            var b = await BestandAsync(client, me, 12, "Zurueck");
            await DeleteAllLocallyAsync(client, b);
            var gesperrt = await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());
            var vorher = await ServerStandAsync(client);

            var vorbereitet = await client.Sync.PrepareDiscardAsync(gesperrt.MassDeletion);
            Assert.AreEqual("12 Fragen und 1 Spiel", vorbereitet.Restored.Description);
            Assert.AreEqual(12 + 4 + 1, vorbereitet.Accompanying, "Schritte, Zellen und Kategorie zählen nicht als mitkommend.");
            Assert.AreEqual(0, vorbereitet.Kept);

            await client.Sync.DiscardAsync(vorbereitet);

            AssertServerUnchanged(vorher, await ServerStandAsync(client));
            await using (var local = client.Local())
            {
                Assert.AreEqual(12, local.QuestionBases.Count(q => b.Fragen.Contains(q.Id)), "Die Fragen sind nicht zurück.");
                Assert.AreEqual(12, local.QuestionStepResources.Count(s => b.Schritte.Contains(s.Id)), "Die Schritte sind nicht zurück.");
                Assert.AreEqual(4, local.GameGridCoordinates.Count(c => b.Zellen.Contains(c.Id)), "Die Zellen sind nicht zurück.");
                Assert.IsTrue(local.Games.Any(g => g.Id == b.Spiel), "Das Spiel ist nicht zurück.");
                Assert.IsTrue(local.Categories.Any(c => c.Id == b.Kategorie), "Die Kategorie ist nicht zurück.");
            }

            Assert.AreEqual(0, await client.Sync.CountPendingAsync(), "Danach hängt noch etwas.");
        }

        /// <summary>
        /// Gemessen statt angenommen: neben der Massenlöschung liegen eine geänderte Frage, eine neue Frage und ein gelöschter
        /// Schritt einer übrigen Frage. Das Verwerfen lässt alle drei offen und schickt sie nicht; erst der nächste Abgleich tut es.
        /// </summary>
        [TestMethod]
        public async Task DiscardingKeepsOtherOpenChanges()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Behalten");
            await using var _ = client;

            var b = await BestandAsync(client, me, 12, "Behalten");
            var uebrige = await OwnQuestionsAsync(client, me, 1, "Uebrig");
            var schritt = new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = uebrige[0], StepText = "Wird gelöscht" };
            await using (var local = client.Local())
            {
                local.QuestionStepResources.Add(schritt);
                await local.SaveChangesAsync();
            }
            await client.Sync.PushAsync();

            var neu = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Neu seit dem Abgleich", CategoryId = await KategorieVonAsync(client, uebrige[0]), OwnerPlayerId = me };
            await using (var local = client.Local())
            {
                local.QuestionBases.Single(q => q.Id == uebrige[0]).Designation = "Lokal geändert";
                local.QuestionStepResources.Remove(local.QuestionStepResources.Single(s => s.Id == schritt.Id));
                local.DefaultQuestions.Add(neu);
                await local.SaveChangesAsync();
            }

            await DeleteAllLocallyAsync(client, b);
            var gesperrt = await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());
            var vorher = await ServerStandAsync(client);

            var vorbereitet = await client.Sync.PrepareDiscardAsync(gesperrt.MassDeletion);
            Assert.AreEqual(3, vorbereitet.Kept, "Die Rückfrage nennt die übrigen offenen Änderungen nicht richtig.");
            await client.Sync.DiscardAsync(vorbereitet);

            AssertServerUnchanged(vorher, await ServerStandAsync(client));
            await using (var local = client.Local())
            {
                Assert.AreEqual(12, local.QuestionBases.Count(q => b.Fragen.Contains(q.Id)), "Die Fragen sind nicht zurück.");
                Assert.AreEqual("Lokal geändert", local.QuestionBases.Single(q => q.Id == uebrige[0]).Designation, "Die lokale Änderung ist weg.");
                Assert.IsTrue(local.QuestionBases.Any(q => q.Id == neu.Id), "Die neue Frage ist weg.");
                Assert.IsFalse(local.QuestionStepResources.Any(s => s.Id == schritt.Id), "Der gelöschte Schritt ist zurück.");
            }

            var danach = await client.Sync.PushAsync();
            Assert.AreEqual(2, danach.Accepted, "Änderung und neue Frage gingen nicht hinaus.");
            Assert.AreEqual(1, danach.Deleted, "Die Löschung des Schritts ging nicht hinaus.");
            Assert.AreEqual(12, await OnServerAsync(client, b.Fragen), "Der Abgleich danach hat die verworfene Löschung doch gesendet.");
        }

        private static async Task<Guid> KategorieVonAsync(ClientHarness client, Guid frage)
        {
            await using var local = client.Local();
            return local.QuestionBases.Single(q => q.Id == frage).CategoryId;
        }

        /// <summary>Der Hintergrund-Abgleich meldet die Sperre, verwirft aber nie von selbst - auch nicht beim zweiten Mal.</summary>
        [TestMethod]
        public async Task TheBackgroundSyncNeverDiscards()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "NieVonSelbst");
            await using var _ = client;

            var b = await BestandAsync(client, me, 12, "NieVonSelbst");
            await DeleteAllLocallyAsync(client, b);

            using var background = new BackgroundSync(client.Sync, TimeSpan.FromMilliseconds(50));
            await background.FlushAsync();
            Assert.IsNotNull(background.Status.PendingMassDeletion, "Die Sperre griff nicht - dann misst dieser Test nichts.");
            await background.FlushAsync();

            await using var local = client.Local();
            Assert.AreEqual(0, local.QuestionBases.Count(q => b.Fragen.Contains(q.Id)), "Der Hintergrund hat die Löschung verworfen.");
            Assert.AreEqual(12, await OnServerAsync(client, b.Fragen), "Der Hintergrund hat gelöscht.");
        }
    }
}
