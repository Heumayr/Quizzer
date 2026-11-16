using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quizzer.DataModels;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Server.Auth;
using Quizzer.Server.Infrastructure;
using System.Net.Http.Json;

[assembly: DoNotParallelize]

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Der Server im Testlauf - gegen eine <b>eigene</b> Datenbank, die am Anfang des Laufs neu
    /// entsteht.
    /// <para>
    /// Woher sie kommt: <c>QUIZZER_SERVER_TEST_SQL</c> (etwa ein SQL Server im Container), sonst
    /// LocalDB. Der Name muss auf <c>_Tests</c> enden - eine Zeichenfolge auf die Spieldatenbank
    /// wird abgewiesen, bevor irgendetwas geloescht wird.
    /// </para>
    /// </summary>
    [TestClass]
    public static class ServerTestHost
    {
        public const string AdminUser = "admin";
        public const string AdminPassword = "Admin-Kennwort-fuer-Tests-1";

        public static string ConnectionString { get; private set; } = string.Empty;

        public static string MediaRoot { get; private set; } = string.Empty;

        /// <summary>
        /// Die TOTP-Uhr aller Server im Lauf (M36 A5). Jeder Code ruft sie einen Schritt weiter -
        /// so kommt der Wiederholschutz nie versehentlich in die Quere, und die Ausweise bleiben
        /// auf der echten Uhr.
        /// </summary>
        public static ManualTimeProvider TotpTime { get; } = new(DateTimeOffset.UtcNow);

        /// <summary>Der Bootstrap-Admin hat TOTP eingerichtet - fuer Admins ist es Pflicht.</summary>
        public static byte[] AdminTotpSecret { get; private set; } = [];

        /// <summary>Fuer Server mit anderem Signierschluessel: dort ist das TOTP-Geheimnis unlesbar.</summary>
        public static Queue<string> AdminRecoveryCodes { get; } = new();

        [AssemblyInitialize]
        public static async Task Init(TestContext _)
        {
            ConnectionString = Environment.GetEnvironmentVariable("QUIZZER_SERVER_TEST_SQL")
                ?? @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer_Server_Tests;Integrated Security=True";

            var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            if (!database.EndsWith("_Tests", StringComparison.Ordinal))
                throw new InvalidOperationException($"Die Testdatenbank muss auf _Tests enden, nicht '{database}'.");

            Settings.ConnectionString = ConnectionString;

            using (var context = new DataContext())
            {
                context.Database.EnsureDeleted();
                context.Database.Migrate();
            }

            MediaRoot = Path.Combine(Path.GetTempPath(), "quizzer-server-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(MediaRoot);

            await using var factory = CreateFactory();
            var first = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(AdminUser, AdminPassword));
            Assert.AreEqual(System.Net.HttpStatusCode.OK, first.StatusCode, await first.Content.ReadAsStringAsync());
            var tokens = (await first.Content.ReadFromJsonAsync<TokenResponse>())!;
            var (secret, confirmed) = await EnableTotpAsync(Authorized(factory, tokens));
            AdminTotpSecret = secret;
            foreach (var code in confirmed.RecoveryCodes)
                AdminRecoveryCodes.Enqueue(code);
        }

        /// <summary>Anmeldung des Bootstrap-Admins mit einem Wiederherstellungscode statt TOTP.</summary>
        public static async Task<HttpClient> AdminWithRecoveryCodeAsync(WebApplicationFactory<Program> factory)
        {
            var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(AdminUser, AdminPassword, AdminRecoveryCodes.Dequeue()));
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return Authorized(factory, (await response.Content.ReadFromJsonAsync<TokenResponse>())!);
        }

        /// <summary>Der Code des naechsten Zeitschritts - die Uhr ruckt dabei einen Schritt weiter.</summary>
        public static string NextCode(byte[] secret)
        {
            TotpTime.Advance(TimeSpan.FromSeconds(Totp.StepSeconds));
            return Totp.Code(secret, Totp.StepAt(TotpTime.GetUtcNow()));
        }

        public static string NextAdminCode() => NextCode(AdminTotpSecret);

        /// <summary>Einrichten und bestaetigen ueber die API, wie es das WPF tut.</summary>
        public static async Task<(byte[] Secret, TotpConfirmResponse Confirmed)> EnableTotpAsync(HttpClient authorized)
        {
            var setup = await authorized.PostAsync("/api/v1/me/totp/setup", null);
            Assert.AreEqual(System.Net.HttpStatusCode.OK, setup.StatusCode, await setup.Content.ReadAsStringAsync());
            var secret = Totp.FromBase32((await setup.Content.ReadFromJsonAsync<TotpSetupResponse>())!.Secret);

            var confirm = await authorized.PostAsJsonAsync("/api/v1/me/totp/confirm", new TotpConfirmRequest(NextCode(secret)));
            Assert.AreEqual(System.Net.HttpStatusCode.OK, confirm.StatusCode, await confirm.Content.ReadAsStringAsync());
            return (secret, (await confirm.Content.ReadFromJsonAsync<TotpConfirmResponse>())!);
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            if (Directory.Exists(MediaRoot))
                Directory.Delete(MediaRoot, recursive: true);
        }

        public static WebApplicationFactory<Program> CreateFactory(int authRequestsPerMinute = 1000,
            IDictionary<string, string?>? extra = null, Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? services = null)
            => new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
            {
                web.UseEnvironment("Development");
                web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Quizzer"] = ConnectionString,
                    ["Quizzer:Jwt:SigningKey"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i * 7 + 3)).ToArray()),
                    ["Quizzer:Bootstrap:AdminUser"] = AdminUser,
                    ["Quizzer:Bootstrap:AdminPassword"] = AdminPassword,
                    ["Quizzer:MediaRoot"] = MediaRoot,
                    ["Quizzer:AuthRequestsPerMinute"] = authRequestsPerMinute.ToString(),
                }));

                if (extra != null)
                    web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(extra));

                web.ConfigureTestServices(s =>
                {
                    s.AddKeyedSingleton<TimeProvider>(ServerSetup.TotpClockKey, TotpTime);
                    services?.Invoke(s);
                });
            });

        /// <summary>Fuer den Bootstrap-Admin kommt der TOTP-Code von selbst mit.</summary>
        public static async Task<TokenResponse> LoginAsync(HttpClient client, string user, string password)
        {
            var code = user == AdminUser && AdminTotpSecret.Length > 0 ? NextAdminCode() : null;
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user, password, code));
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        }

        public static HttpClient Authorized(WebApplicationFactory<Program> factory, TokenResponse tokens)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
            return client;
        }

        /// <summary>Legt ueber den Admin ein Mitgliedskonto an und meldet es an.</summary>
        public static async Task<(HttpClient Client, TokenResponse Tokens)> NewMemberAsync(WebApplicationFactory<Program> factory, string name)
        {
            var admin = Authorized(factory, await LoginAsync(factory.CreateClient(), AdminUser, AdminPassword));
            var userName = name + "-" + Guid.NewGuid().ToString("N")[..6];
            const string password = "Mitglieds-Kennwort-123";

            var created = await admin.PostAsJsonAsync("/api/v1/accounts",
                new CreateAccountRequest(userName, password, DataModels.Enumerations.AccountRole.Member, null, name));
            Assert.AreEqual(System.Net.HttpStatusCode.OK, created.StatusCode, await created.Content.ReadAsStringAsync());

            var tokens = await LoginAsync(factory.CreateClient(), userName, password);
            return (Authorized(factory, tokens), tokens);
        }
    }

    /// <summary>Eine Uhr, die nur auf Zuruf weitergeht.</summary>
    public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
