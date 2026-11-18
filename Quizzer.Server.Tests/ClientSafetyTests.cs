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
    public sealed partial class ClientSafetyTests
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
        /// aus, als sei alles geloescht. Das darf nicht zum Server. Mit Fragen, nicht mit Kategorien:
        /// die loescht ein Mitglied am Server ohnehin nicht, und seit V06 zaehlen sie nicht mehr mit.
        /// </summary>
        [TestMethod]
        public async Task AWrongLocalDatabaseDoesNotWipeTheServer()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Wipe");
            await using var _ = client;

            var fragen = await OwnQuestionsAsync(client, me, 12, "K");

            // Jetzt "zeigt" die lokale Datenbank woanders hin: alles weg.
            await using (var local = client.Local())
            {
                local.QuestionBases.RemoveRange(local.QuestionBases);
                local.Categories.RemoveRange(local.Categories);
                await local.SaveChangesAsync();
            }

            await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => client.Sync.PushAsync());

            Assert.AreEqual(12, await OnServerAsync(client, fragen), "Die Fragen sind am Server nicht mehr alle da.");
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
        /// der Abgleich steckt nicht fest. Kaputt ist ein Platz, den ein anderes Geraet desselben Kontos
        /// schon belegt hat: jede Zeile besteht die Pruefung, erst die Datenbank weist den Stapel ab (409),
        /// und nur der Einzelzeilen-Rueckfall bringt den Rest hinein (M37 P9, P7-1 - vorher scheiterte die
        /// Zelle schon an der Zeilenpruefung, und der Test blieb gruen, wenn der Rueckfall warf).
        /// </summary>
        [TestMethod]
        public async Task OneBrokenRowDoesNotBlockTheRest()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, me) = await SignedInAsync(factory, "Stapel");
            await using var _ = client;

            var game = new Game { Id = Guid.NewGuid(), Designation = "Spiel", ModeratorPlayerId = me };
            await using (var local = client.Local())
            {
                local.Games.Add(game);
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await client.Sync.PushAsync()).Rejected.Count);

            // Das andere Geraet hat sich schon in das Spiel gesetzt - am PC noch nicht abgeholt.
            var anderswo = await client.Api.PushAsync(new SyncPushRequest
            {
                Upserts = [SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = me })],
            });
            Assert.AreEqual(0, anderswo.Rejected.Count);

            var good = new Category { Id = Guid.NewGuid(), Designation = "Gut" };
            var second = new Game { Id = Guid.NewGuid(), Designation = "Zweites Spiel", ModeratorPlayerId = me };
            var doppelt = new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = me };
            await using (var local = client.Local())
            {
                local.AddRange(good, second, doppelt);
                await local.SaveChangesAsync();
            }

            var result = await client.Sync.PushAsync();

            Assert.AreEqual(doppelt.Id, result.Rejected.Single().Id, "Nur der doppelte Platz bleibt draussen.");
            Assert.AreEqual(SyncRejectReason.Invalid, result.Rejected.Single().Reason, "Der Platz kam nicht als gescheiterte Einzelzeile zurueck.");
            Assert.AreEqual(2, result.Accepted, "Kategorie und zweites Spiel sind angekommen.");
            Assert.IsFalse((await client.Sync.PushAsync()).HadChanges, "Und beim naechsten Mal haengt nichts mehr.");
        }
    }
}
