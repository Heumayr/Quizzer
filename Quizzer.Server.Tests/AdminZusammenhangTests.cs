using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// V09 (M37 D2): beim Admin entfallen nur die Rechte, der Zusammenhang einer Zeile gilt auch für ihn. Gemessen
    /// am 2026-11-16: ein Ergebnis und ein Joker aus Spiel A an einer Zelle von Spiel B wurden vom Admin angenommen -
    /// danach brach Ginas Abholen bei jeder Anmeldung ab, und Bob konnte B nicht mehr löschen.
    /// </summary>
    [TestClass]
    public sealed class AdminZusammenhangTests
    {
        private const string Kennwort = "Mitglieds-Kennwort-123";

        /// <summary>Gina leitet A mit Max und Moritz (Gäste) am Tisch; Gustav ist Gast ohne Platz; Bob leitet B.</summary>
        private sealed record Lage(
            HttpClient Admin, HttpClient Bob, string GinaUser, Guid MaxId, Guid MoritzId, Guid GustavId,
            SyncTestData.GameSet A, SyncTestData.GameSet B);

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> factory)
            => ServerTestHost.Authorized(factory,
                await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

        private static async Task<Lage> LageAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, ginaTokens) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");
            var admin = await AdminAsync(factory);

            var a = SyncTestData.NewGame("Ginas A");
            var max = new Player { Id = Guid.NewGuid(), Designation = "Max", DisplayName = "Max" };
            var moritz = new Player { Id = Guid.NewGuid(), Designation = "Moritz", DisplayName = "Moritz" };
            var gustav = new Player { Id = Guid.NewGuid(), Designation = "Gustav", DisplayName = "Gustav" };

            var push = a.AsNewPush();
            push.Upserts.AddRange([SyncTestData.New(max), SyncTestData.New(moritz), SyncTestData.New(gustav)]);
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = max.Id }));
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = moritz.Id }));
            Assert.AreEqual(0, (await PushAsync(gina, push)).Rejected.Count, "Ginas Spiel wurde nicht angelegt.");

            var b = SyncTestData.NewGame("Bobs B");
            Assert.AreEqual(0, (await PushAsync(bob, b.AsNewPush())).Rejected.Count, "Bobs Spiel wurde nicht angelegt.");

            return new Lage(admin, bob, ginaTokens.Account.UserName, max.Id, moritz.Id, gustav.Id, a, b);
        }

        private static PlayerJoker Joker(Lage lage, Guid spieler, JokerKind art) => new()
        {
            Id = Guid.NewGuid(), GameId = lage.A.Game.Id, PlayerId = spieler, Kind = art, Reason = "Admin-Korrektur",
            GrantedAt = DateTimeOffset.UtcNow,
        };

        private static QuestionResult Ergebnis(Lage lage, Guid zelle) => new()
        {
            Id = Guid.NewGuid(), GameId = lage.A.Game.Id, QuestionBaseId = lage.A.Question.Id, PlayerId = lage.MaxId,
            GameGridCoordinateId = zelle, Score = 100,
        };

        private static void AssertForbidden(SyncPushResponse result, Guid id, string warum)
            => Assert.IsTrue(result.Rejected.Any(r => r.Id == id && r.Reason == SyncRejectReason.Forbidden), warum);

        private static void AssertAccepted(SyncPushResponse result, Guid id, string warum)
            => Assert.IsTrue(result.Accepted.Any(a => a.Id == id),
                warum + " " + string.Join("; ", result.Rejected.Select(r => r.Message)));

        /// <summary>
        /// Ergebnis und Joker aus A an einer Zelle von B: abgewiesen. Danach löscht Bob B, und Gina holt ab. Die
        /// richtige Zeile im selben Push geht durch - in Ginas Spiel schreiben darf der Admin weiterhin.
        /// </summary>
        [TestMethod]
        public async Task TheAdminCannotHangAResultOrAJokerOnACellOfAnotherGame()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var lage = await LageAsync(factory);

            var fremdesErgebnis = Ergebnis(lage, lage.B.Cell.Id);
            var fremderJoker = Joker(lage, lage.MaxId, JokerKind.Shield);
            fremderJoker.GameGridCoordinateId = lage.B.Cell.Id;
            fremderJoker.RedeemedAt = DateTimeOffset.UtcNow;
            var richtig = Ergebnis(lage, lage.A.Cell.Id);

            var result = await PushAsync(lage.Admin, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(fremdesErgebnis), SyncTestData.New(fremderJoker), SyncTestData.New(richtig)],
            });

            AssertForbidden(result, fremdesErgebnis.Id, "Der Admin hängte ein Ergebnis aus A an eine Zelle von B.");
            AssertForbidden(result, fremderJoker.Id, "Der Admin hängte einen Joker aus A an eine Zelle von B.");
            AssertAccepted(result, richtig.Id, "Ein richtiges Ergebnis in Ginas Spiel darf der Admin schreiben.");

            var hash = (await SnapshotAsync(lage.Bob)).Rows.Single(r => r.Id == lage.B.Game.Id).Hash;
            var geloescht = await PushAsync(lage.Bob, new SyncPushRequest
            {
                Deletes = [new SyncDelete { Type = nameof(Game), Id = lage.B.Game.Id, BaseHash = hash }],
            });
            Assert.AreEqual(0, geloescht.Rejected.Count, string.Join("; ", geloescht.Rejected.Select(r => r.Message)));
            Assert.IsFalse((await SnapshotAsync(lage.Bob)).Rows.Any(r => r.Id == lage.B.Game.Id), "Bobs Spiel steht noch da.");

            await using var ginasRechner = await ClientHarness.CreateAsync(factory, "Gina");
            await ginasRechner.Api.LoginAsync(lage.GinaUser, Kennwort);
            await ginasRechner.Sync.PullAsync();

            await using var lokal = ginasRechner.Local();
            Assert.IsTrue(await lokal.Games.AnyAsync(g => g.Id == lage.A.Game.Id), "Ginas Abholen kam nicht bis zu ihrem Spiel.");
        }

        /// <summary>
        /// Die Joker-Regeln gelten auch für den Admin: Spieler und Sperr-Ziel sitzen im Spiel, ein Ziel hat nur die
        /// Sperre und nie gegen sich selbst, die Art ist bekannt. Die richtige Sperre geht durch.
        /// </summary>
        [TestMethod]
        public async Task TheAdminIsHeldToTheJokerRules()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var lage = await LageAsync(factory);

            var ohnePlatz = Joker(lage, lage.GustavId, JokerKind.Double);
            var zielOhnePlatz = Joker(lage, lage.MaxId, JokerKind.Block);
            zielOhnePlatz.TargetPlayerId = lage.GustavId;
            var gegenSich = Joker(lage, lage.MaxId, JokerKind.Block);
            gegenSich.TargetPlayerId = lage.MaxId;
            var zielOhneSperre = Joker(lage, lage.MaxId, JokerKind.Double);
            zielOhneSperre.TargetPlayerId = lage.MoritzId;
            var unbekannt = Joker(lage, lage.MaxId, (JokerKind)99);
            var richtig = Joker(lage, lage.MaxId, JokerKind.Block);
            richtig.TargetPlayerId = lage.MoritzId;

            var result = await PushAsync(lage.Admin, new SyncPushRequest
            {
                Upserts = [.. new[] { ohnePlatz, zielOhnePlatz, gegenSich, zielOhneSperre, unbekannt, richtig }.Select(SyncTestData.New)],
            });

            AssertForbidden(result, ohnePlatz.Id, "Der Admin gab einem Gast ohne Platz einen Joker.");
            AssertForbidden(result, zielOhnePlatz.Id, "Der Admin sperrte einen Gast ohne Platz.");
            AssertForbidden(result, gegenSich.Id, "Der Admin schrieb eine Sperre gegen sich selbst.");
            AssertForbidden(result, zielOhneSperre.Id, "Der Admin schrieb ein Doppelt mit Ziel.");
            AssertForbidden(result, unbekannt.Id, "Der Admin schrieb eine unbekannte Joker-Art.");
            AssertAccepted(result, richtig.Id, "Die richtige Sperre in Ginas Spiel darf der Admin schreiben.");
        }

        /// <summary>
        /// Die Gegenrichtung: die Rechte entfallen beim Admin weiterhin. Ein bestehendes Design ändern ist die einzige
        /// Rechteprüfung, die nicht schon in den Helfern für ihn aufgeht - ohne diese Zusicherung blieb der ganze
        /// Serverlauf grün, als der Admin wie ein Mitglied geprüft wurde (gemessen bei V09).
        /// </summary>
        [TestMethod]
        public async Task TheAdminStillChangesAnExistingTheme()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var admin = await AdminAsync(factory);

            var design = new GameTheme { Id = Guid.NewGuid(), Designation = "Abend", FolderName = "Abend" };
            Assert.AreEqual(0, (await PushAsync(alice, new SyncPushRequest { Upserts = [SyncTestData.New(design)] })).Rejected.Count);

            var hash = (await SnapshotAsync(admin)).Rows.Single(r => r.Id == design.Id).Hash;
            design.FolderName = "Standard";
            var result = await PushAsync(admin, new SyncPushRequest { Upserts = [SyncTestData.Changed(design, hash)] });

            AssertAccepted(result, design.Id, "Ein bestehendes Design ändert der Admin.");
        }
    }
}
