using Microsoft.EntityFrameworkCore;
using Quizzer.Client;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>Die Funde 1 und 4 der Durchsicht vom 2026-10-21, am Client.</summary>
    [TestClass]
    public sealed class ClientSafetyTests
    {
        private static async Task<(ClientHarness Client, Guid Me)> SignedInAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string name)
        {
            var (_, tokens) = await ServerTestHost.NewMemberAsync(factory, name);
            var client = await ClientHarness.CreateAsync(factory, name);
            await client.Api.LoginAsync(tokens.Account.UserName, "Mitglieds-Kennwort-123");
            await client.Sync.PullAsync();
            return (client, tokens.Account.PlayerId);
        }

        /// <summary>
        /// Fund 1, zweite Linie: liest der Abgleich gegen eine fremde (leere) Datenbank, sieht es
        /// aus, als sei alles geloescht. Das darf nicht zum Server.
        /// </summary>
        [TestMethod]
        public async Task AWrongLocalDatabaseDoesNotWipeTheServer()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Wipe");
            await using var _ = client;

            await using (var local = client.Local())
            {
                for (var i = 0; i < 12; i++)
                    local.Categories.Add(new Category { Id = Guid.NewGuid(), Designation = "K" + i });
                await local.SaveChangesAsync();
            }
            await client.Sync.PushAsync();

            // Jetzt "zeigt" die lokale Datenbank woanders hin: alles weg.
            await using (var local = client.Local())
            {
                local.Categories.RemoveRange(local.Categories);
                await local.SaveChangesAsync();
            }

            await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());

            var snapshot = await client.Api.GetSnapshotAsync();
            Assert.IsTrue(snapshot.Rows.Count(r => r.Type == nameof(Category) && r.Values["Designation"].GetString()!.StartsWith('K')) >= 12,
                "Die Kategorien sind am Server noch da.");
        }

        /// <summary>Ein Spiel mit vielen Zellen zu loeschen ist kein Verdachtsfall.</summary>
        [TestMethod]
        public async Task DeletingOneGameWithManyCellsIsAllowed()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Viele");
            await using var _ = client;

            var game = new Game { Id = Guid.NewGuid(), Designation = "Gross", ModeratorPlayerId = me };
            await using (var local = client.Local())
            {
                local.Games.Add(game);
                for (var i = 0; i < 25; i++)
                    local.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = game.Id, X = i % 5, Y = i / 5 });
                await local.SaveChangesAsync();
            }
            await client.Sync.PushAsync();

            await using (var local = client.Local())
            {
                local.GameGridCoordinates.RemoveRange(local.GameGridCoordinates.Where(c => c.GameId == game.Id));
                local.Games.Remove(await local.Games.SingleAsync(g => g.Id == game.Id));
                await local.SaveChangesAsync();
            }

            var result = await client.Sync.PushAsync();
            Assert.AreEqual(26, result.Deleted);
        }

        /// <summary>
        /// Fund 4: passt ein Stapel am Server nicht zusammen, geht der Rest trotzdem hinein, und
        /// der Abgleich steckt nicht fest.
        /// </summary>
        [TestMethod]
        public async Task OneBrokenRowDoesNotBlockTheRest()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Stapel");
            await using var _ = client;

            var good = new Category { Id = Guid.NewGuid(), Designation = "Gut" };
            var game = new Game { Id = Guid.NewGuid(), Designation = "Spiel", ModeratorPlayerId = me };
            await using (var local = client.Local())
            {
                local.Categories.Add(good);
                local.Games.Add(game);
                await local.SaveChangesAsync();
            }

            // Eine Zelle, deren Frage es am Server nicht gibt: lokal mit abgeschalteter Pruefung angelegt.
            await using (var local = client.Local())
            {
                await local.Database.ExecuteSqlRawAsync("ALTER TABLE [base].[GameGridCoordinate] NOCHECK CONSTRAINT ALL");
                local.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = game.Id, QuestionBaseId = Guid.NewGuid() });
                await local.SaveChangesAsync();
                await local.Database.ExecuteSqlRawAsync("ALTER TABLE [base].[GameGridCoordinate] CHECK CONSTRAINT ALL");
            }

            var result = await client.Sync.PushAsync();

            Assert.AreEqual(1, result.Rejected.Count, "Nur die kaputte Zelle bleibt draussen.");
            Assert.AreEqual(2, result.Accepted, "Kategorie und Spiel sind angekommen.");
            Assert.IsFalse((await client.Sync.PushAsync()).HadChanges, "Und beim naechsten Mal haengt nichts mehr.");
        }
    }
}
