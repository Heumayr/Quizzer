using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>Was die Code-Durchsicht vom 2026-10-21 gefunden hat - jeder Fund mit seinem Test.</summary>
    [TestClass]
    public sealed class ReviewFindingsTests
    {
        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        /// <summary>Fund 2: korrigiert der Admin ein fremdes Spiel, bleibt der Gamemaster, wer er war.</summary>
        [TestMethod]
        public async Task AnAdminCorrectionKeepsGameMasterAndOwner()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, aliceTokens) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var set = SyncTestData.NewGame("Admin");
            await PushAsync(alice, set.AsNewPush());

            var snapshot = await SnapshotAsync(admin);
            var gameRow = snapshot.Rows.Single(r => r.Id == set.Game.Id);
            var questionRow = snapshot.Rows.Single(r => r.Id == set.Question.Id);

            set.Game.ModeratorPlayerId = aliceTokens.Account.PlayerId;
            set.Game.Designation = "Vom Admin korrigiert";
            set.Question.OwnerPlayerId = aliceTokens.Account.PlayerId;
            set.Question.QuestionText = "Vom Admin korrigiert";

            var result = await PushAsync(admin, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(set.Game, gameRow.Hash), SyncTestData.Changed(set.Question, questionRow.Hash)],
            });
            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));

            var after = await SnapshotAsync(alice);
            Assert.AreEqual(aliceTokens.Account.PlayerId, after.Rows.Single(r => r.Id == set.Game.Id).Values["ModeratorPlayerId"].GetGuid());
            Assert.AreEqual(aliceTokens.Account.PlayerId, after.Rows.Single(r => r.Id == set.Question.Id).Values["OwnerPlayerId"].GetGuid());
            Assert.AreEqual("Vom Admin korrigiert", after.Rows.Single(r => r.Id == set.Game.Id).Values["Designation"].GetString());
        }

        /// <summary>Fund 7: eine Zelle im eigenen Spiel darf nicht auf eine fremde private Frage zeigen.</summary>
        [TestMethod]
        public async Task ACellCannotPointAtSomeoneElsesPrivateQuestion()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");

            var secret = SyncTestData.NewGame("Geheim");
            await PushAsync(alice, secret.AsNewPush());

            // Bob baut ein eigenes Spiel und laesst eine Zelle auf Alices Frage zeigen.
            var mine = SyncTestData.NewGame("Bobs");
            var push = mine.AsNewPush();
            var thief = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = mine.Game.Id, QuestionBaseId = secret.Question.Id, X = 1, Y = 0 };
            push.Upserts.Add(SyncTestData.New(thief));

            var result = await PushAsync(bob, push);

            Assert.IsTrue(result.Rejected.Any(r => r.Id == thief.Id && r.Reason == SyncRejectReason.Forbidden), "Die Zelle muss abgewiesen werden.");
            Assert.IsFalse((await SnapshotAsync(bob)).Rows.Any(r => r.Id == secret.Question.Id), "Bob sieht Alices Frage nicht.");
        }

        /// <summary>Fund 7, Gegenrichtung: auf eine geteilte Frage darf man verweisen.</summary>
        [TestMethod]
        public async Task ACellMayPointAtASharedQuestion()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, aliceTokens) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");

            var shared = SyncTestData.NewGame("Geteilt");
            shared.Question.IsShared = true;
            shared.Question.OwnerPlayerId = aliceTokens.Account.PlayerId;
            await PushAsync(alice, shared.AsNewPush());

            var mine = SyncTestData.NewGame("Bobs");
            var push = mine.AsNewPush();
            var cell = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = mine.Game.Id, QuestionBaseId = shared.Question.Id, X = 1, Y = 0 };
            push.Upserts.Add(SyncTestData.New(cell));

            var result = await PushAsync(bob, push);
            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
        }

        /// <summary>Fund 10: ein herabgestufter Admin verliert die Rechte sofort.</summary>
        [TestMethod]
        public async Task ADemotedAdminLosesRightsImmediately()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var name = "zweiter-admin-" + Guid.NewGuid().ToString("N")[..6];
            var created = await admin.PostAsJsonAsync("/api/v1/accounts", new CreateAccountRequest(name, "Zweiter-Admin-Kennwort-1", AccountRole.Admin, null, "Zweiter"));
            var second = (await created.Content.ReadFromJsonAsync<AccountListItem>())!;

            var secondClient = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), name, "Zweiter-Admin-Kennwort-1"));
            Assert.AreEqual(HttpStatusCode.OK, (await secondClient.GetAsync("/api/v1/accounts")).StatusCode);

            await admin.PutAsJsonAsync($"/api/v1/accounts/{second.AccountId}", new UpdateAccountRequest(null, AccountRole.Member, null));

            Assert.AreEqual(HttpStatusCode.Unauthorized, (await secondClient.GetAsync("/api/v1/accounts")).StatusCode,
                "Der alte Ausweis mit Admin-Rolle gilt nicht mehr.");
        }
    }
}
