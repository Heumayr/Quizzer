using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Die Zwei-Faktor-Anmeldung im WPF (M36 A5) gegen einen Ersatzserver: Codefeld bei der
    /// Anmeldung, Pflicht-Einrichtung fuer Admins, der Einrichtungsdialog.
    /// </summary>
    [TestClass]
    public class ZweiFaktorAnmeldungUnitTests
    {
        private Func<bool> originalSetup = null!;

        [TestInitialize]
        public void SetUp()
        {
            originalSetup = ServerLoginViewModel.ShowTwoFactorSetup;
            UserPrompt.Current = new ThrowingUserPrompt();
        }

        [TestCleanup]
        public void TearDown()
        {
            ServerLoginViewModel.ShowTwoFactorSetup = originalSetup;
            ServerSession.UseApiForTests(null);
            UserPrompt.Reset();
        }

        private static TokenResponse Tokens(AccountRole role, bool totp) => new(
            "zugang", DateTimeOffset.UtcNow.AddMinutes(15), "erneuern", DateTimeOffset.UtcNow.AddDays(14),
            new AccountInfo(Guid.NewGuid(), "anna", role, Guid.NewGuid(), "Anna", totp));

        private static QuizzerApiClient Api(ErsatzServer server)
        {
            var api = new QuizzerApiClient(new Uri("https://quizzer.example.org/api/v1/"), new MemoryTokenStore(), handler: server);
            ServerSession.UseApiForTests(api);
            return api;
        }

        [TestMethod]
        public async Task ACodeFieldAppearsWhenTheServerAsksForIt()
        {
            var server = new ErsatzServer((pfad, rumpf) => pfad == "/api/v1/auth/login" && !rumpf.Contains("\"totpCode\":\"")
                ? ErsatzServer.Json(HttpStatusCode.Unauthorized, new ApiError("Bitte den Code eingeben.", ApiErrorCodes.TotpRequired))
                : ErsatzServer.Json(HttpStatusCode.Unauthorized, new ApiError("Benutzername oder Kennwort falsch.")));
            Api(server);
            var vm = new ServerLoginViewModel { UserName = "anna", Password = "Kennwort-123456" };

            Assert.AreEqual(Visibility.Collapsed, vm.CodeVisibility);
            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            Assert.AreEqual(Visibility.Visible, vm.CodeVisibility);
            StringAssert.Contains(vm.Error, "Code");

            vm.Code = "123 456";
            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            StringAssert.Contains(server.Anfragen[^1].Rumpf, "\"totpCode\":\"123 456\"");
            Assert.IsFalse(vm.SignedIn);
        }

        [TestMethod]
        public async Task AnAdminWhoCancelsTheSetupIsNotSignedIn()
        {
            var server = new ErsatzServer((pfad, _) => pfad == "/api/v1/auth/login"
                ? ErsatzServer.Json(HttpStatusCode.OK, Tokens(AccountRole.Admin, totp: false))
                : new HttpResponseMessage(HttpStatusCode.NoContent));
            var api = Api(server);
            var aufrufe = 0;
            ServerLoginViewModel.ShowTwoFactorSetup = () => { aufrufe++; return false; };
            var vm = new ServerLoginViewModel { UserName = "anna", Password = "Kennwort-123456" };

            await ((AsyncRelayCommand)vm.SignInCommand).ExecuteAsync(null);

            Assert.AreEqual(1, aufrufe, "Die Einrichtung wurde nicht angeboten.");
            Assert.IsFalse(vm.SignedIn);
            Assert.AreEqual(ServerLoginViewModel.AdminNeedsTwoFactor, vm.Error);
            CollectionAssert.Contains(server.Anfragen.Select(a => a.Pfad).ToList(), "/api/v1/auth/logout", "Der eingeschränkte Ausweis bleibt liegen.");
            Assert.IsFalse(api.IsSignedIn);
        }

        [TestMethod]
        public async Task TheSetupDialogShowsQrAndSecretAndThenTheRecoveryCodes()
        {
            string[] codes = ["AAAA-BBBB-CCCC-DDDD", "EEEE-FFFF-GGGG-HHHH"];
            var server = new ErsatzServer((pfad, _) => pfad switch
            {
                "/api/v1/auth/login" => ErsatzServer.Json(HttpStatusCode.OK, Tokens(AccountRole.Member, totp: false)),
                "/api/v1/me/totp/setup" => ErsatzServer.Json(HttpStatusCode.OK,
                    new TotpSetupResponse("JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP", "otpauth://totp/Quizzer%3Aanna?secret=JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP&issuer=Quizzer")),
                "/api/v1/me/totp/confirm" => ErsatzServer.Json(HttpStatusCode.OK, new TotpConfirmResponse(codes, Tokens(AccountRole.Member, totp: true))),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
            var api = Api(server);
            await api.LoginAsync("anna", "Kennwort-123456");

            var vm = new TwoFactorViewModel();
            await vm.LoadForTestAsync();

            Assert.AreEqual(TwoFactorViewModel.Stage.Setup, vm.Current, vm.Message);
            Assert.AreEqual("JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP", vm.Secret);
            Assert.IsNotNull(vm.QrImage);

            vm.Code = "123456";
            await ((AsyncRelayCommand)vm.ConfirmCommand).ExecuteAsync(null);

            Assert.AreEqual(TwoFactorViewModel.Stage.Codes, vm.Current, vm.Message);
            foreach (var c in codes)
                StringAssert.Contains(vm.RecoveryCodes, c);
            Assert.IsTrue(vm.Activated);
            Assert.IsTrue(api.Account!.TotpEnabled, "Die neuen Ausweise mit dem Anspruch wurden nicht übernommen.");
        }

        [TestMethod]
        [DataRow(AccountRole.Member, TwoFactorViewModel.Stage.Disable)]
        [DataRow(AccountRole.Admin, TwoFactorViewModel.Stage.Required)]
        public async Task WithTotpAMemberMaySwitchItOffAnAdminMayNot(AccountRole role, TwoFactorViewModel.Stage expected)
        {
            var server = new ErsatzServer((pfad, _) => ErsatzServer.Json(HttpStatusCode.OK, Tokens(role, totp: true)));
            var api = Api(server);
            await api.LoginAsync("anna", "Kennwort-123456", "123456");

            var vm = new TwoFactorViewModel();
            await vm.LoadForTestAsync();

            Assert.AreEqual(expected, vm.Current);
            Assert.AreEqual(1, server.Anfragen.Count, "Mit eingerichtetem TOTP wird keine neue Einrichtung begonnen.");
        }
    }

    /// <summary>Antwortet nach Pfad und Rumpf und merkt sich jede Anfrage.</summary>
    internal sealed class ErsatzServer(Func<string, string, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        public List<(string Pfad, string Rumpf)> Anfragen { get; } = new();

        public static HttpResponseMessage Json(HttpStatusCode status, object body)
            => new(status) { Content = JsonContent.Create(body, body.GetType(), options: QuizzerApiClient.Json) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var rumpf = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Anfragen.Add((request.RequestUri!.AbsolutePath, rumpf));
            return antwort(request.RequestUri.AbsolutePath, rumpf);
        }
    }
}
