using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    [TestClass]
    public sealed class AuthTests
    {
        [TestMethod]
        public async Task VersionIsAnonymousButSnapshotIsNot()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var client = factory.CreateClient();

            Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/version")).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/sync/snapshot")).StatusCode);
        }

        [TestMethod]
        public async Task UnknownUserAndWrongPasswordLookTheSame()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var client = factory.CreateClient();

            var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("gibt-es-nicht", "irgendwas-langes-1"));
            var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(ServerTestHost.AdminUser, "falsches-Kennwort-1"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, unknown.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.AreEqual(await unknown.Content.ReadAsStringAsync(), await wrong.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task TooManyFailuresLockTheAccountEvenForTheRightPassword()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var userName = "sperre-" + Guid.NewGuid().ToString("N")[..6];
            await admin.PostAsJsonAsync("/api/v1/accounts", new CreateAccountRequest(userName, "Richtiges-Kennwort-1", AccountRole.Member, null, "Sperre"));

            var client = factory.CreateClient();
            for (var i = 0; i < 5; i++)
                await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, "falsch-falsch-falsch"));

            var right = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, "Richtiges-Kennwort-1"));
            Assert.AreEqual(HttpStatusCode.Unauthorized, right.StatusCode);
        }

        [TestMethod]
        public async Task RefreshRotatesAndAReusedTokenKillsTheWholeFamily()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var client = factory.CreateClient();
            var first = await ServerTestHost.LoginAsync(client, ServerTestHost.AdminUser, ServerTestHost.AdminPassword);

            var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
            Assert.AreEqual(HttpStatusCode.OK, rotated.StatusCode);
            var second = (await rotated.Content.ReadFromJsonAsync<TokenResponse>())!;
            Assert.AreNotEqual(first.RefreshToken, second.RefreshToken);

            // Der alte Schluessel taucht wieder auf - er ist abgegriffen.
            var reused = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
            Assert.AreEqual(HttpStatusCode.Unauthorized, reused.StatusCode);

            // Und der Nachfolger, den der Angreifer womoeglich haelt, gilt auch nicht mehr.
            var successor = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
            Assert.AreEqual(HttpStatusCode.Unauthorized, successor.StatusCode);
        }

        [TestMethod]
        public async Task ATamperedTokenIsRejected()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var tokens = await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword);

            var parts = tokens.AccessToken.Split('.');
            var tampered = parts[0] + "." + parts[1] + "." + new string(parts[2].Reverse().ToArray());

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", tampered);

            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        }

        [TestMethod]
        public async Task AMemberCannotManageAccounts()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (member, _) = await ServerTestHost.NewMemberAsync(factory, "Mitglied");

            Assert.AreEqual(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/accounts")).StatusCode);
            var create = await member.PostAsJsonAsync("/api/v1/accounts", new CreateAccountRequest("boese", "Kennwort-fuer-Boese-1", AccountRole.Admin, null, "Boese"));
            Assert.AreEqual(HttpStatusCode.Forbidden, create.StatusCode);
        }

        [TestMethod]
        public async Task ADisabledAccountLosesAccessImmediately()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));
            var (member, tokens) = await ServerTestHost.NewMemberAsync(factory, "Gesperrt");

            Assert.AreEqual(HttpStatusCode.OK, (await member.GetAsync("/api/v1/me")).StatusCode);

            var update = await admin.PutAsJsonAsync($"/api/v1/accounts/{tokens.Account.AccountId}", new UpdateAccountRequest(null, null, true));
            Assert.AreEqual(HttpStatusCode.NoContent, update.StatusCode);

            Assert.AreEqual(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/me")).StatusCode);
        }

        [TestMethod]
        public async Task ShortPasswordsAreRefused()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var response = await admin.PostAsJsonAsync("/api/v1/accounts", new CreateAccountRequest("kurz-" + Guid.NewGuid().ToString("N")[..4], "kurz", AccountRole.Member, null, "Kurz"));
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task LoginIsRateLimitedPerAddress()
        {
            await using var factory = ServerTestHost.CreateFactory(authRequestsPerMinute: 3);
            var client = factory.CreateClient();

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 5; i++)
                statuses.Add((await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("x", "y-y-y-y-y-y-y"))).StatusCode);

            CollectionAssert.Contains(statuses, HttpStatusCode.TooManyRequests);
        }
    }
}
