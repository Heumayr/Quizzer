using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Quizzer.DataModels;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
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

        [AssemblyInitialize]
        public static void Init(TestContext _)
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
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            if (Directory.Exists(MediaRoot))
                Directory.Delete(MediaRoot, recursive: true);
        }

        public static WebApplicationFactory<Program> CreateFactory(int authRequestsPerMinute = 1000)
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
            });

        public static async Task<TokenResponse> LoginAsync(HttpClient client, string user, string password)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user, password));
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
}
