using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server.Updates;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Aktualisierung des WPF ueber den Server (M36 A7): Paketinfo und Paket ohne Anmeldung, hinter
    /// der Anmeldebremse, am Versionsriegel vorbei - und nur aus <c>client/</c> im Namensmuster.
    /// </summary>
    [TestClass]
    public sealed class ClientPackageTests
    {
        private const string Older = "20261101-1200-aaaaaaa";
        private const string Newer = "20261116-1530-bbbbbbb";

        private string mediaRoot = string.Empty;

        [TestInitialize]
        public void SetUp()
        {
            mediaRoot = Path.Combine(Path.GetTempPath(), "quizzer-paket-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(mediaRoot);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(mediaRoot))
                Directory.Delete(mediaRoot, recursive: true);
        }

        private WebApplicationFactory<Program> Factory(int authRequestsPerMinute = 1000)
            => ServerTestHost.CreateFactory(authRequestsPerMinute, new Dictionary<string, string?> { ["Quizzer:MediaRoot"] = mediaRoot });

        private byte[] Place(string relativePath, int size = 4096)
        {
            var bytes = RandomNumberGenerator.GetBytes(size);
            var full = Path.Combine(mediaRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
            return bytes;
        }

        private byte[] PlacePackage(string version, int size = 4096)
            => Place(Path.Combine(ClientPackageStore.Folder, $"Quizzer-{version}.zip"), size);

        private static HttpClient WithFormat(WebApplicationFactory<Program> factory, string? format)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Remove(ApiInfo.SyncFormatHeader);
            if (format != null)
                client.DefaultRequestHeaders.Add(ApiInfo.SyncFormatHeader, format);
            return client;
        }

        [TestMethod]
        public async Task WithoutAPackageBothRoutesAnswer404()
        {
            await using var factory = Factory();
            var client = factory.CreateClient();

            Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync(ClientPackageEndpoints.LatestPath)).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync(ClientPackageEndpoints.PackagePath)).StatusCode);
        }

        /// <summary>Ohne Ausweis: gerade ein abgewiesenes altes WPF kommt an keinen.</summary>
        [TestMethod]
        public async Task AnyoneGetsVersionChecksumAndPackageWithoutSigningIn()
        {
            var bytes = PlacePackage(Newer);
            await using var factory = Factory();
            var client = factory.CreateClient();

            var latest = await client.GetAsync(ClientPackageEndpoints.LatestPath);
            Assert.AreEqual(HttpStatusCode.OK, latest.StatusCode, await latest.Content.ReadAsStringAsync());
            var info = (await latest.Content.ReadFromJsonAsync<ClientPackageInfo>())!;

            var package = await client.GetAsync(ClientPackageEndpoints.PackagePath);
            Assert.AreEqual(HttpStatusCode.OK, package.StatusCode);
            var served = await package.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(Newer, info.Version);
            Assert.AreEqual(bytes.LongLength, info.Size);
            CollectionAssert.AreEqual(bytes, served);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(served)), info.Sha256);
            Assert.AreEqual("no-store", package.Headers.CacheControl?.ToString());
        }

        /// <summary>Ein ausgetauschtes Paket bekommt eine neue Pruefsumme, nicht die zwischengespeicherte.</summary>
        [TestMethod]
        public async Task AReplacedPackageGetsANewChecksum()
        {
            PlacePackage(Newer, size: 1000);
            await using var factory = Factory();
            var client = factory.CreateClient();
            var first = (await client.GetFromJsonAsync<ClientPackageInfo>(ClientPackageEndpoints.LatestPath))!;

            var bytes = PlacePackage(Newer, size: 2000);
            var second = (await client.GetFromJsonAsync<ClientPackageInfo>(ClientPackageEndpoints.LatestPath))!;

            Assert.AreNotEqual(first.Sha256, second.Sha256);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(bytes)), second.Sha256);
            Assert.AreEqual(2000, second.Size);
        }

        [TestMethod]
        public async Task OfSeveralPackagesTheNewestIsServed()
        {
            PlacePackage(Older);
            var newest = PlacePackage(Newer);
            PlacePackage("20261110-0800-ccccccc");
            await using var factory = Factory();
            var client = factory.CreateClient();

            var info = (await client.GetFromJsonAsync<ClientPackageInfo>(ClientPackageEndpoints.LatestPath))!;

            Assert.AreEqual(Newer, info.Version);
            CollectionAssert.AreEqual(newest, await client.GetByteArrayAsync(ClientPackageEndpoints.PackagePath));
        }

        /// <summary>
        /// Positivliste: nur <c>client/Quizzer-&lt;kanonische Version&gt;.zip</c>. Weder ein anderer Ordner
        /// noch ein Unterordner, ein halb kopiertes <c>.neu</c> oder ein Pfad aus der Anfrage erweitert das.
        /// </summary>
        [TestMethod]
        public async Task OnlyCanonicalPackagesInTheClientFolderAreServedAndNoRequestPathWidensThat()
        {
            var later = "20991231-2359-";
            Place($"Quizzer-{later}fffffff.zip");
            Place($"Resources/Quizzer-{later}eeeeeee.zip");
            Place($"{ClientPackageStore.Folder}/sub/Quizzer-{later}ddddddd.zip");
            Place($"{ClientPackageStore.Folder}/Quizzer-{later}ccccccc.zip.neu");
            Place($"{ClientPackageStore.Folder}/Quizzer-{later}ABCDEF0.zip");
            Place($"{ClientPackageStore.Folder}/Quizzer-{later}bbbbbbb+x.zip");
            Place($"{ClientPackageStore.Folder}/Quizzer-unsinn.zip");
            Place($"{ClientPackageStore.Folder}/notizen.txt");
            await using var factory = Factory();
            var client = factory.CreateClient();

            Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync(ClientPackageEndpoints.LatestPath)).StatusCode,
                "Ein Name außerhalb des Musters wurde als Paket angeboten.");

            var valid = PlacePackage(Older);

            Assert.AreEqual(Older, (await client.GetFromJsonAsync<ClientPackageInfo>(ClientPackageEndpoints.LatestPath))!.Version);
            CollectionAssert.AreEqual(valid, await client.GetByteArrayAsync(ClientPackageEndpoints.PackagePath + "?path=../Quizzer-" + later + "fffffff.zip"));

            foreach (var tail in new[] { "/notizen.txt", "/..%2F..%2FQuizzer-" + later + "fffffff.zip", "/sub" })
                Assert.AreNotEqual(HttpStatusCode.OK, (await client.GetAsync(ClientPackageEndpoints.PackagePath + tail)).StatusCode, tail);
        }

        [TestMethod]
        public async Task TheLoginBrakeAlsoLimitsTheUpdateRoutes()
        {
            PlacePackage(Newer);
            await using var factory = Factory(authRequestsPerMinute: 2);
            var client = factory.CreateClient();

            foreach (var path in new[] { ClientPackageEndpoints.LatestPath, ClientPackageEndpoints.PackagePath })
            {
                var statuses = new List<HttpStatusCode>();
                for (var i = 0; i < 3; i++)
                    statuses.Add((await client.GetAsync(path)).StatusCode);

                CollectionAssert.Contains(statuses, HttpStatusCode.TooManyRequests, path);
            }
        }

        /// <summary>Gerade das Programm, das der Versionsriegel abweist, muss sich das neue holen koennen.</summary>
        [TestMethod]
        [DataRow(null)]
        [DataRow("1")]
        public async Task TheFormatGateLetsAnOldProgramFetchTheUpdate(string? format)
        {
            PlacePackage(Newer);
            await using var factory = Factory();
            var old = WithFormat(factory, format);

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, (await old.GetAsync(ApiInfo.BasePath + "/me")).StatusCode,
                "Der Riegel greift hier gar nicht - dann prüft dieser Test nichts.");
            Assert.AreEqual(HttpStatusCode.OK, (await old.GetAsync(ClientPackageEndpoints.LatestPath)).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await old.GetAsync(ClientPackageEndpoints.PackagePath)).StatusCode);
        }

        [TestMethod]
        public async Task TheClientReadsTheInfoAndDownloadsThePackage()
        {
            await using var factory = Factory();
            using var api = new QuizzerApiClient(new Uri("https://localhost/api/v1/"), new MemoryTokenStore(), handler: factory.Server.CreateHandler());

            Assert.IsNull(await api.GetClientPackageAsync());
            Assert.IsFalse(await api.DownloadClientPackageAsync(new MemoryStream()));

            var bytes = PlacePackage(Newer, size: 200_000);
            var reported = new List<long>();
            using var target = new MemoryStream();

            var info = await api.GetClientPackageAsync();
            Assert.IsTrue(await api.DownloadClientPackageAsync(target, new SynchronousProgress(reported.Add)));

            Assert.AreEqual(Newer, info!.Version);
            CollectionAssert.AreEqual(bytes, target.ToArray());
            Assert.AreEqual(bytes.LongLength, reported[^1], string.Join(",", reported.Select(r => r.ToString(CultureInfo.InvariantCulture))));
        }

        /// <summary><see cref="Progress{T}"/> meldet ueber den Threadpool - fuer die Zusicherung zu spaet.</summary>
        private sealed class SynchronousProgress(Action<long> report) : IProgress<long>
        {
            public void Report(long value) => report(value);
        }
    }
}
