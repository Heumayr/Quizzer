using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// V15 (M37 D3): eine bestehende Zeile bleibt in ihrem Spiel - Zelle, Kopfzeile, Platz, Ergebnis und Joker. Gemessen
    /// am 2026-11-16: wer Gamemaster zweier Spiele ist (oder Admin), verschob eine Zelle samt Ergebnis von A nach A2, und
    /// danach ließ sich A2 nicht mehr löschen (409, „passen nicht zusammen").
    /// </summary>
    [TestClass]
    public sealed class SpielzugehoerigkeitTests
    {
        /// <summary>
        /// Gina leitet A und A2; Max sitzt in beiden, Moritz nur in A. In A eine Kopfzeile, ein Joker und zwei Zellen mit je
        /// einem Ergebnis - die erste wandert allein, die zweite mit ihrem Ergebnis.
        /// </summary>
        private sealed record Lage(HttpClient Gina, SyncTestData.GameSet A, SyncTestData.GameSet A2,
            Header Kopfzeile, PlayerXGame MoritzInA, PlayerJoker Joker, GameGridCoordinate Zelle2, QuestionResult Ergebnis2);

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<Dictionary<Guid, string>> HashesAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!.Rows.ToDictionary(r => r.Id, r => r.Hash);

        private static async Task<Lage> LageAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaUmzug");
            var a = SyncTestData.NewGame("A");
            var a2 = SyncTestData.NewGame("A2");
            var max = new Player { Id = Guid.NewGuid(), Designation = "Max", DisplayName = "Max" };
            var moritz = new Player { Id = Guid.NewGuid(), Designation = "Moritz", DisplayName = "Moritz" };

            var kopfzeile = new Header { Id = Guid.NewGuid(), GameId = a.Game.Id, HeaderType = HeaderType.Column, Index = 0 };
            var moritzInA = new PlayerXGame { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = moritz.Id };
            QuestionResult Ergebnis(GameGridCoordinate zelle) => new()
            {
                Id = Guid.NewGuid(), GameId = a.Game.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = a.Question.Id, PlayerId = max.Id, Score = 100,
            };
            var zelle2 = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = a.Game.Id, QuestionBaseId = a.Question.Id, X = 1, Y = 0 };
            var ergebnis2 = Ergebnis(zelle2);
            var joker = new PlayerJoker { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = max.Id, Kind = JokerKind.Double, Reason = "Umzug", GrantedAt = DateTimeOffset.UtcNow };

            var push = a.AsNewPush();
            push.Upserts.AddRange(a2.AsNewPush().Upserts);
            push.Upserts.AddRange([SyncTestData.New(max), SyncTestData.New(moritz)]);
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = max.Id }));
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = a2.Game.Id, PlayerId = max.Id }));
            push.Upserts.AddRange([SyncTestData.New(kopfzeile), SyncTestData.New(moritzInA), SyncTestData.New(joker), SyncTestData.New(zelle2)]);
            push.Upserts.AddRange([SyncTestData.New(Ergebnis(a.Cell)), SyncTestData.New(ergebnis2)]);

            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            return new Lage(gina, a, a2, kopfzeile, moritzInA, joker, zelle2, ergebnis2);
        }

        [TestMethod]
        public Task AGameMasterOfBothGamesCannotMoveRows() => NoRowLeavesItsGameAsync(admin: false);

        [TestMethod]
        public Task TheAdminCannotMoveRowsEither() => NoRowLeavesItsGameAsync(admin: true);

        /// <summary>
        /// Je Zeilenart ein eigener Push mit geänderter <c>GameId</c>, dazu Zelle und Ergebnis zusammen - jeder abgewiesen.
        /// Vorher lässt sich eine Zelle, die im Spiel bleibt, ändern; danach lässt sich A2 löschen - mit der allein
        /// verschobenen Zelle scheiterte das, weil ihr Ergebnis in A auf sie zeigt.
        /// </summary>
        private static async Task NoRowLeavesItsGameAsync(bool admin)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var lage = await LageAsync(factory);
            var schreiber = admin
                ? ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword))
                : lage.Gina;
            var nachA2 = lage.A2.Game.Id;

            // Zuerst die Gegenrichtung: eine Zelle, die im Spiel bleibt, lässt sich weiter ändern.
            var bleibt = Kopie(lage.A.Cell, c => c.Phase = 2);
            var geaendert = await PushAsync(schreiber, new SyncPushRequest { Upserts = [SyncTestData.Changed(bleibt, (await HashesAsync(schreiber))[bleibt.Id])] });
            Assert.IsTrue(geaendert.Accepted.Any(a => a.Id == bleibt.Id), "Eine Zelle, die im Spiel bleibt, ließ sich nicht ändern.");

            var hashes = await HashesAsync(schreiber);
            var zelle = Kopie(bleibt, c => c.GameId = nachA2);
            var faelle = new (string Name, Guid Id, SyncUpsert[] Zeilen)[]
            {
                ("Zelle", zelle.Id, [SyncTestData.Changed(zelle, hashes[zelle.Id])]),
                ("Kopfzeile", lage.Kopfzeile.Id, [SyncTestData.Changed(Kopie(lage.Kopfzeile, h => h.GameId = nachA2), hashes[lage.Kopfzeile.Id])]),
                ("Platz", lage.MoritzInA.Id, [SyncTestData.Changed(Kopie(lage.MoritzInA, p => p.GameId = nachA2), hashes[lage.MoritzInA.Id])]),
                ("Joker", lage.Joker.Id, [SyncTestData.Changed(Kopie(lage.Joker, j => j.GameId = nachA2), hashes[lage.Joker.Id])]),
                ("Ergebnis mit seiner Zelle", lage.Ergebnis2.Id,
                    [SyncTestData.Changed(Kopie(lage.Zelle2, c => c.GameId = nachA2), hashes[lage.Zelle2.Id]),
                     SyncTestData.Changed(Kopie(lage.Ergebnis2, r => r.GameId = nachA2), hashes[lage.Ergebnis2.Id])]),
            };

            var verschoben = new List<string>();
            foreach (var (name, id, zeilen) in faelle)
            {
                var result = await PushAsync(schreiber, new SyncPushRequest { Upserts = [.. zeilen] });
                if (!result.Rejected.Any(r => r.Id == id && r.Reason == SyncRejectReason.Forbidden))
                    verschoben.Add(name);
            }

            Assert.AreEqual(0, verschoben.Count, "In ein anderes Spiel verschoben: " + string.Join(", ", verschoben));

            var geloescht = await PushAsync(lage.Gina, new SyncPushRequest
            {
                Deletes = [new SyncDelete { Type = nameof(Game), Id = nachA2, BaseHash = (await HashesAsync(lage.Gina))[nachA2] }],
            });
            Assert.AreEqual(0, geloescht.Rejected.Count, "A2 ließ sich nicht löschen: " + string.Join("; ", geloescht.Rejected.Select(r => r.Message)));
        }

        /// <summary>Eine Kopie mit Änderung - das Original bleibt der Stand am Server.</summary>
        private static T Kopie<T>(T zeile, Action<T> aendern) where T : class
        {
            var kopie = (T)typeof(T).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(zeile, null)!;
            aendern(kopie);
            return kopie;
        }
    }
}
