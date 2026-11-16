using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Die Aktualisierung im Anmeldefenster (M36 A7) gegen einen Ersatzserver: angeboten wird ein
    /// neueres Paket beim Öffnen und nach „zu alt" (426), nie einem Entwicklungsbau; der Knopf lädt,
    /// prüft, entpackt und startet die neue Version.
    /// </summary>
    [TestClass]
    public class AktualisierungAnmeldungUnitTests
    {
        private const string TooOld = "Dieses Quizzer-Programm ist zu alt für den Server. Bitte Quizzer aktualisieren.";

        private string? originalVersion;
        private string originalRoot = string.Empty;
        private Action<string> originalStart = null!;
        private string folder = string.Empty;
        private readonly List<string> started = new();

        [TestInitialize]
        public void SetUp()
        {
            originalVersion = ClientUpdate.RunningVersion;
            originalRoot = ClientUpdate.Root;
            originalStart = ClientUpdate.StartAndExit;

            folder = Path.Combine(Path.GetTempPath(), "quizzer-anmeldung-update-" + Guid.NewGuid().ToString("N"));
            ClientUpdate.Root = Path.Combine(folder, "app");
            ClientUpdate.RunningVersion = ClientUpdateUnitTests.Older;
            ClientUpdate.StartAndExit = started.Add;
            UserPrompt.Current = new ThrowingUserPrompt();
        }

        [TestCleanup]
        public void TearDown()
        {
            ClientUpdate.RunningVersion = originalVersion;
            ClientUpdate.Root = originalRoot;
            ClientUpdate.StartAndExit = originalStart;
            ServerSession.UseApiForTests(null);
            UserPrompt.Reset();

            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        private static QuizzerApiClient Api(ErsatzServer server)
        {
            var api = new QuizzerApiClient(new Uri("https://quizzer.example.org/api/v1/"), new MemoryTokenStore(), handler: server);
            ServerSession.UseApiForTests(api);
            return api;
        }

        /// <summary>Ein Server mit Paket, der jede Anmeldung als zu alt abweist.</summary>
        private static ErsatzServer Server(ClientPackageInfo? info, byte[] package, HttpStatusCode login = HttpStatusCode.UpgradeRequired)
            => new((pfad, _) => pfad switch
            {
                "/api/v1/version" => ErsatzServer.Json(HttpStatusCode.OK, new VersionResponse(1, 2, "test")),
                "/api/v1/auth/login" => ErsatzServer.Json(login, new ApiError(TooOld)),
                "/api/v1/client/latest" when info != null => ErsatzServer.Json(HttpStatusCode.OK, info),
                "/api/v1/client/package" when info != null => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) },
                _ => ErsatzServer.Json(HttpStatusCode.NotFound, new ApiError("Nicht gefunden.")),
            });

        private static ServerLoginViewModel SignInForm() => new() { UserName = "anna", Password = "Kennwort-123456" };

        private static (ClientPackageInfo Info, byte[] Bytes) Package(string version = ClientUpdateUnitTests.Newer)
        {
            var bytes = ClientUpdateUnitTests.Zip(ClientUpdate.ExeName, "Quizzer.dll");
            return (ClientUpdateUnitTests.InfoFor(version, bytes), bytes);
        }

        [TestMethod]
        public async Task AProgramTheServerCallsTooOldIsOfferedTheUpdate()
        {
            var (info, bytes) = Package();
            Api(Server(info, bytes));
            var vm = SignInForm();

            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            Assert.AreEqual(TooOld, vm.Error);
            Assert.AreEqual(Visibility.Visible, vm.UpdateVisibility);
            StringAssert.StartsWith(vm.UpdateText, "Neue Version verfügbar (");
            StringAssert.EndsWith(vm.UpdateText, ") – herunterladen und neu starten");
            Assert.IsFalse(vm.SignedIn);
        }

        [TestMethod]
        public async Task OpeningTheWindowOffersANewerPackageButNotTheSameOne()
        {
            var (info, bytes) = Package();
            var api = Api(Server(info, bytes));
            var vm = SignInForm();

            await vm.ProbeAsync(api);
            Assert.AreEqual(Visibility.Visible, vm.UpdateVisibility);
            Assert.AreEqual(string.Empty, vm.Error);

            ClientUpdate.RunningVersion = ClientUpdateUnitTests.Newer;
            await vm.ProbeAsync(api);
            Assert.AreEqual(Visibility.Collapsed, vm.UpdateVisibility);
        }

        [TestMethod]
        public async Task ADevelopmentBuildIsOfferedNothingEvenWhenTooOld()
        {
            ClientUpdate.RunningVersion = ClientUpdateUnitTests.DevelopmentBuild;
            var (info, bytes) = Package();
            var api = Api(Server(info, bytes));
            var vm = SignInForm();

            await vm.ProbeAsync(api);
            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            Assert.AreEqual(Visibility.Collapsed, vm.UpdateVisibility);
            Assert.AreEqual(TooOld, vm.Error);
        }

        /// <summary>Ein Server ohne die Paketwege (vor A7) antwortet 401 - das Fenster bleibt, wie es war.</summary>
        [TestMethod]
        public async Task AServerWithoutTheUpdateRoutesChangesNothing()
        {
            var api = Api(new ErsatzServer((pfad, _) => pfad == "/api/v1/version"
                ? ErsatzServer.Json(HttpStatusCode.OK, new VersionResponse(1, 2, "alt"))
                : ErsatzServer.Json(HttpStatusCode.Unauthorized, new ApiError("Nicht angemeldet."))));
            var vm = SignInForm();

            await vm.ProbeAsync(api);

            Assert.AreEqual(Visibility.Collapsed, vm.UpdateVisibility);
            Assert.AreEqual(string.Empty, vm.Error);
        }

        /// <summary>A6 bleibt: ein ausgeschalteter Server meldet sich verständlich, ein Angebot gibt es nicht.</summary>
        [TestMethod]
        public async Task AServerThatIsDownStillSaysSo()
        {
            var api = Api(new ErsatzServer((_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "weg")));
            var vm = SignInForm();

            await vm.ProbeAsync(api);

            StringAssert.StartsWith(vm.Error, QuizzerApiClient.Unreachable);
            Assert.AreEqual(Visibility.Collapsed, vm.UpdateVisibility);
        }

        [TestMethod]
        public async Task TheButtonInstallsAndStartsTheNewVersion()
        {
            var (info, bytes) = Package();
            Api(Server(info, bytes));
            var vm = SignInForm();
            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            await ((AsyncRelayCommand)vm.UpdateCommand).ExecuteAsync(null);

            var exe = Path.Combine(ClientUpdate.Root, ClientUpdateUnitTests.Newer, ClientUpdate.ExeName);
            CollectionAssert.AreEqual(new[] { exe }, started, vm.Error);
            Assert.IsTrue(ClientUpdate.IsComplete(Path.GetDirectoryName(exe)!));
            Assert.IsFalse(vm.SignedIn);
        }

        [TestMethod]
        public async Task AFailedDownloadSaysWhyAndStartsNothing()
        {
            var (info, bytes) = Package();
            Api(Server(info with { Sha256 = new string('0', 64) }, bytes));
            var vm = SignInForm();
            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            await ((AsyncRelayCommand)vm.UpdateCommand).ExecuteAsync(null);

            Assert.AreEqual(0, started.Count);
            StringAssert.Contains(vm.Error, ClientUpdate.ChecksumMismatch);
            Assert.AreEqual(Visibility.Collapsed, vm.BusyVisibility);
        }
    }
}
