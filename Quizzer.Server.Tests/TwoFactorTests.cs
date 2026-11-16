using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Auth;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Logic.Sync;
using Quizzer.Server.Auth;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Zwei-Faktor-Anmeldung am Server (M36 A5, F-M36-02): je Konto einstellbar, Pflicht fuer
    /// Admins. Die Missbrauchsfaelle stehen je als eigener Test.
    /// </summary>
    [TestClass]
    public sealed class TwoFactorTests
    {
        private const string Password = "Zwei-Faktor-Kennwort-1";

        private static async Task<(HttpClient Client, TokenResponse Tokens, string UserName)> NewAccountAsync(WebApplicationFactory<Program> factory, AccountRole role)
        {
            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));
            var userName = "zf-" + Guid.NewGuid().ToString("N")[..8];

            var created = await admin.PostAsJsonAsync("/api/v1/accounts", new CreateAccountRequest(userName, Password, role, null, userName));
            Assert.AreEqual(HttpStatusCode.OK, created.StatusCode, await created.Content.ReadAsStringAsync());

            var tokens = await ServerTestHost.LoginAsync(factory.CreateClient(), userName, Password);
            return (ServerTestHost.Authorized(factory, tokens), tokens, userName);
        }

        private static Task<HttpResponseMessage> LoginAsync(WebApplicationFactory<Program> factory, string user, string? code, string password = Password)
            => factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user, password, code));

        private static async Task<string?> ErrorCodeOf(HttpResponseMessage response)
            => (await response.Content.ReadFromJsonAsync<ApiError>())?.Code;

        /// <summary>Ein falscher Code im Format des richtigen: die erste Ziffer verschoben.</summary>
        private static string Wrong(string code) => (char)('0' + (code[0] - '0' + 1) % 10) + code[1..];

        private static string CodeAt(byte[] secret, int stepsFromNow)
            => Totp.Code(secret, Totp.StepAt(ServerTestHost.TotpTime.GetUtcNow()) + stepsFromNow);

        [TestMethod]
        public async Task WithTotpTheRightPasswordAloneAsksForTheCode()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (secret, _) = await ServerTestHost.EnableTotpAsync(client);

            var ohne = await LoginAsync(factory, user, null);
            Assert.AreEqual(HttpStatusCode.Unauthorized, ohne.StatusCode);
            Assert.AreEqual(ApiErrorCodes.TotpRequired, await ErrorCodeOf(ohne));

            var code = ServerTestHost.NextCode(secret);
            var falsch = await LoginAsync(factory, user, Wrong(code));
            var falschesKennwort = await LoginAsync(factory, user, null, "ganz-falsches-Kennwort");
            Assert.AreEqual(HttpStatusCode.Unauthorized, falsch.StatusCode);
            Assert.AreEqual(await falschesKennwort.Content.ReadAsStringAsync(), await falsch.Content.ReadAsStringAsync(),
                "Ein falscher Code verraet nicht mehr als ein falsches Kennwort.");

            var richtig = await LoginAsync(factory, user, code);
            Assert.AreEqual(HttpStatusCode.OK, richtig.StatusCode);
            Assert.IsTrue((await richtig.Content.ReadFromJsonAsync<TokenResponse>())!.Account.TotpEnabled);
        }

        /// <summary>Falsche Codes sperren wie falsche Kennwoerter - und Anfragen ohne Code setzen die Zaehlung nicht zurueck.</summary>
        [TestMethod]
        public async Task WrongCodesCountTowardsTheLockout()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (secret, _) = await ServerTestHost.EnableTotpAsync(client);

            for (var i = 0; i < 5; i++)
            {
                Assert.AreEqual(ApiErrorCodes.TotpRequired, await ErrorCodeOf(await LoginAsync(factory, user, null)));
                Assert.AreEqual(HttpStatusCode.Unauthorized, (await LoginAsync(factory, user, Wrong(ServerTestHost.NextCode(secret)))).StatusCode);
            }

            var richtig = await LoginAsync(factory, user, ServerTestHost.NextCode(secret));
            Assert.AreEqual(HttpStatusCode.Unauthorized, richtig.StatusCode, "Nach fünf falschen Codes ist das Konto gesperrt.");
        }

        [TestMethod]
        public async Task ACodeCountsOnlyOnce()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (secret, _) = await ServerTestHost.EnableTotpAsync(client);

            var code = ServerTestHost.NextCode(secret);
            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(factory, user, code)).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await LoginAsync(factory, user, code)).StatusCode, "derselbe Code zweimal");

            // Ein spaeterer Schritt verbraucht die frueheren mit, auch die nie benutzten im Fenster.
            ServerTestHost.TotpTime.Advance(TimeSpan.FromMinutes(5));
            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(factory, user, CodeAt(secret, +1))).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await LoginAsync(factory, user, CodeAt(secret, 0))).StatusCode, "frueherer Schritt nach spaeterem");
        }

        /// <summary>Derselbe Code gleichzeitig mehrfach eingereicht - hoechstens eine Anmeldung gelingt.</summary>
        [TestMethod]
        public async Task AParallelLoginWithOneCodeSucceedsAtMostOnce()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (secret, confirmed) = await ServerTestHost.EnableTotpAsync(client);

            var code = ServerTestHost.NextCode(secret);
            var antworten = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => LoginAsync(factory, user, code)));
            Assert.IsTrue(antworten.Count(a => a.StatusCode == HttpStatusCode.OK) <= 1,
                $"{antworten.Count(a => a.StatusCode == HttpStatusCode.OK)} Anmeldungen mit einem Code.");

            var wiederherstellung = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => LoginAsync(factory, user, confirmed.RecoveryCodes[0])));
            Assert.IsTrue(wiederherstellung.Count(a => a.StatusCode == HttpStatusCode.OK) <= 1,
                $"{wiederherstellung.Count(a => a.StatusCode == HttpStatusCode.OK)} Anmeldungen mit einem Wiederherstellungscode.");
        }

        [TestMethod]
        public async Task ARecoveryCodeWorksExactlyOnce()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (_, confirmed) = await ServerTestHost.EnableTotpAsync(client);

            Assert.AreEqual(10, confirmed.RecoveryCodes.Distinct().Count());

            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(factory, user, confirmed.RecoveryCodes[0])).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await LoginAsync(factory, user, confirmed.RecoveryCodes[0])).StatusCode);

            var abgetippt = confirmed.RecoveryCodes[1].Replace("-", " ").ToLowerInvariant();
            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(factory, user, abgetippt)).StatusCode, "klein und mit Leerzeichen abgetippt");
        }

        [TestMethod]
        public async Task AnAdminWithoutTotpMayOnlySetItUp()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (admin, tokens, _) = await NewAccountAsync(factory, AccountRole.Admin);
            Assert.IsFalse(tokens.Account.TotpEnabled);

            foreach (var path in new[] { "/api/v1/accounts", "/api/v1/sync/snapshot", "/api/v1/me/statistics", "/api/v1/media", "/api/v1/ki/einstellungen" })
            {
                var response = await admin.GetAsync(path);
                Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, path);
                Assert.AreEqual(ApiErrorCodes.TotpSetupRequired, await ErrorCodeOf(response), path);
            }

            var push = await admin.PostAsJsonAsync("/api/v1/sync/push", new { });
            Assert.AreEqual(HttpStatusCode.Forbidden, push.StatusCode, "Abgleich schreiben");

            Assert.AreEqual(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/me")).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await admin.PostAsync("/api/v1/me/totp/setup", null)).StatusCode);
        }

        [TestMethod]
        public async Task AfterConfirmingTheAdminHasFullRightsAndOtherSessionsEnd()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (admin, first, user) = await NewAccountAsync(factory, AccountRole.Admin);
            var other = await ServerTestHost.LoginAsync(factory.CreateClient(), user, Password);

            var (_, confirmed) = await ServerTestHost.EnableTotpAsync(admin);
            var full = ServerTestHost.Authorized(factory, confirmed.Tokens);

            Assert.IsTrue(confirmed.Tokens.Account.TotpEnabled);
            Assert.AreEqual(HttpStatusCode.OK, (await full.GetAsync("/api/v1/accounts")).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await full.GetAsync("/api/v1/sync/snapshot")).StatusCode);

            var client = factory.CreateClient();
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(other.RefreshToken))).StatusCode, "die andere Sitzung");
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken))).StatusCode, "die eigene alte");

            // Erneuern braucht keinen Code und traegt den aktuellen Stand weiter.
            var renewed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(confirmed.Tokens.RefreshToken));
            Assert.AreEqual(HttpStatusCode.OK, renewed.StatusCode);
            var again = ServerTestHost.Authorized(factory, (await renewed.Content.ReadFromJsonAsync<TokenResponse>())!);
            Assert.AreEqual(HttpStatusCode.OK, (await again.GetAsync("/api/v1/accounts")).StatusCode);
        }

        [TestMethod]
        public async Task AMemberWithoutTotpKeepsFullRights()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (member, tokens, _) = await NewAccountAsync(factory, AccountRole.Member);

            Assert.IsFalse(tokens.Account.TotpEnabled);
            Assert.AreEqual(HttpStatusCode.OK, (await member.GetAsync("/api/v1/sync/snapshot")).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await member.GetAsync("/api/v1/me/statistics")).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await member.GetAsync("/api/v1/media")).StatusCode);
        }

        [TestMethod]
        public async Task TheSecretIsNotStoredInPlainText()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (member, tokens, _) = await NewAccountAsync(factory, AccountRole.Member);

            var setup = (await (await member.PostAsync("/api/v1/me/totp/setup", null)).Content.ReadFromJsonAsync<TotpSetupResponse>())!;
            var secret = Totp.FromBase32(setup.Secret);
            Assert.AreEqual(Totp.SecretBytes, secret.Length);
            StringAssert.Contains(setup.OtpAuthUri, "secret=" + setup.Secret);

            await using var context = new DataContext();
            var stored = await context.Accounts.AsNoTracking().Where(a => a.Id == tokens.Account.AccountId).Select(a => a.TotpSecretProtected).SingleAsync();

            Assert.IsNotNull(stored);
            Assert.IsFalse(stored.Contains(setup.Secret, StringComparison.OrdinalIgnoreCase), "Base32 im Klartext");
            Assert.IsFalse(stored.Contains(Convert.ToHexString(secret), StringComparison.OrdinalIgnoreCase), "Hex im Klartext");
            Assert.IsFalse(stored.Contains(Convert.ToBase64String(secret)), "Base64 im Klartext");

            var bytes = Convert.FromBase64String(stored[(stored.IndexOf(':') + 1)..]);
            Assert.IsFalse(bytes.AsSpan().IndexOf(secret) >= 0, "die rohen Bytes stehen im gespeicherten Wert");
        }

        [TestMethod]
        public async Task AMemberCanSwitchItOffAnAdminCannot()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (secret, confirmed) = await ServerTestHost.EnableTotpAsync(client);
            var member = ServerTestHost.Authorized(factory, confirmed.Tokens);

            var ohneKennwort = await member.PostAsJsonAsync("/api/v1/me/totp/disable", new TotpDisableRequest("falsches-Kennwort-1", ServerTestHost.NextCode(secret)));
            Assert.AreEqual(HttpStatusCode.BadRequest, ohneKennwort.StatusCode);

            var aus = await member.PostAsJsonAsync("/api/v1/me/totp/disable", new TotpDisableRequest(Password, ServerTestHost.NextCode(secret)));
            Assert.AreEqual(HttpStatusCode.NoContent, aus.StatusCode, await aus.Content.ReadAsStringAsync());
            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(factory, user, null)).StatusCode, "danach wieder ohne Code");

            var admin = ServerTestHost.Authorized(factory, await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));
            var adminAus = await admin.PostAsJsonAsync("/api/v1/me/totp/disable", new TotpDisableRequest(ServerTestHost.AdminPassword, ServerTestHost.NextAdminCode()));
            Assert.AreEqual(HttpStatusCode.Forbidden, adminAus.StatusCode);
        }

        [TestMethod]
        public async Task TheAdminResetsAMembersTotp()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, tokens, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (_, confirmed) = await ServerTestHost.EnableTotpAsync(client);
            var member = ServerTestHost.Authorized(factory, confirmed.Tokens);

            var adminTokens = await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword);
            var admin = ServerTestHost.Authorized(factory, adminTokens);

            Assert.AreEqual(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/v1/accounts/{adminTokens.Account.AccountId}/totp")).StatusCode, "ein Mitglied setzt nichts zurück");
            Assert.AreEqual(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/v1/accounts/{adminTokens.Account.AccountId}/totp")).StatusCode, "nicht das eigene");
            Assert.AreEqual(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/accounts/{tokens.Account.AccountId}/totp")).StatusCode);

            Assert.AreEqual(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/me")).StatusCode, "der Ausweis mit dem alten Stand");
            var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(confirmed.Tokens.RefreshToken));
            Assert.AreEqual(HttpStatusCode.Unauthorized, refresh.StatusCode, "die Sitzungen des Kontos");
            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(factory, user, null)).StatusCode, "wieder ohne Code");
        }

        /// <summary>
        /// Ein neuer JWT-Signierschluessel macht jedes TOTP-Geheimnis unlesbar: Codes gelten nicht
        /// mehr, die Wiederherstellungscodes (nur gehasht, ohne Schluessel) schon.
        /// </summary>
        [TestMethod]
        public async Task AChangedSigningKeyLeavesOnlyTheRecoveryCodes()
        {
            string user;
            byte[] secret;
            TotpConfirmResponse confirmed;
            await using (var factory = ServerTestHost.CreateFactory())
            {
                (var client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
                (secret, confirmed) = await ServerTestHost.EnableTotpAsync(client);
            }

            var anderer = new Dictionary<string, string?>
            {
                ["Quizzer:Jwt:SigningKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(200 - i)).ToArray()),
            };
            await using var neu = ServerTestHost.CreateFactory(extra: anderer);

            Assert.AreEqual(HttpStatusCode.Unauthorized, (await LoginAsync(neu, user, ServerTestHost.NextCode(secret))).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await LoginAsync(neu, user, confirmed.RecoveryCodes[0])).StatusCode);
        }

        /// <summary>Mit TOTP ginge das Neuanmelden nach dem Kennwortwechsel nicht ohne Code - die Ausweise kommen mit.</summary>
        [TestMethod]
        public async Task ChangingThePasswordHandsOutNewTokens()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, _) = await NewAccountAsync(factory, AccountRole.Member);
            var (_, confirmed) = await ServerTestHost.EnableTotpAsync(client);
            var member = ServerTestHost.Authorized(factory, confirmed.Tokens);

            var changed = await member.PostAsJsonAsync("/api/v1/me/password", new ChangePasswordRequest(Password, "Ein-ganz-neues-Kennwort-2"));
            Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode, await changed.Content.ReadAsStringAsync());
            var tokens = (await changed.Content.ReadFromJsonAsync<TokenResponse>())!;

            var refresh = factory.CreateClient();
            Assert.AreEqual(HttpStatusCode.OK, (await refresh.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken))).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await refresh.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(confirmed.Tokens.RefreshToken))).StatusCode, "die alte Sitzung");
        }

        /// <summary>Konten, Erneuerungsschluessel und Wiederherstellungscodes wandern nie zu einem Client.</summary>
        [TestMethod]
        public void AuthTablesStayOutOfTheSync()
        {
            var authTypes = typeof(Account).Assembly.GetTypes()
                .Where(t => t.IsClass && t.Namespace == typeof(Account).Namespace)
                .ToList();

            CollectionAssert.Contains(authTypes, typeof(AccountRecoveryCode));
            foreach (var type in authTypes)
                Assert.IsFalse(SyncModel.TryGet(type.Name, out _), type.Name);

            Assert.IsFalse(SyncModel.Entries.Any(e => e.ClrType.Namespace == typeof(Account).Namespace));
        }

        /// <summary>Der Weg des WPF: der Client erkennt „Code nötig" und „Einrichtung nötig" an eigenen Ausnahmen.</summary>
        [TestMethod]
        public async Task TheClientTellsCodeNeededFromSetupNeeded()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (client, _, user) = await NewAccountAsync(factory, AccountRole.Member);
            var (secret, _) = await ServerTestHost.EnableTotpAsync(client);
            var (_, _, adminUser) = await NewAccountAsync(factory, AccountRole.Admin);

            var store = new Quizzer.Client.MemoryTokenStore();
            using var api = new Quizzer.Client.QuizzerApiClient(new Uri("https://localhost/api/v1/"), store, handler: factory.Server.CreateHandler());

            await Assert.ThrowsExactlyAsync<Quizzer.Client.TotpRequiredException>(() => api.LoginAsync(user, Password));
            await api.LoginAsync(user, Password, ServerTestHost.NextCode(secret));
            Assert.IsTrue(api.Account!.TotpEnabled);

            // Offline-Start und Wiederverbindung erneuern mit dem gespeicherten Schluessel - ohne Code.
            using var resumed = new Quizzer.Client.QuizzerApiClient(new Uri("https://localhost/api/v1/"), store, handler: factory.Server.CreateHandler());
            Assert.IsTrue(await resumed.TryResumeAsync());
            await resumed.GetSnapshotAsync();

            using var adminApi = new Quizzer.Client.QuizzerApiClient(new Uri("https://localhost/api/v1/"), new Quizzer.Client.MemoryTokenStore(), handler: factory.Server.CreateHandler());
            await adminApi.LoginAsync(adminUser, Password);
            await Assert.ThrowsExactlyAsync<Quizzer.Client.TotpSetupRequiredException>(() => adminApi.GetSnapshotAsync());
        }
    }
}
