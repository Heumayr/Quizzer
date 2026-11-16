using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Der Server hinter dem Tunnel (M36 A6): eine Adresse ohne Port mit oeffentlichem Zertifikat,
    /// und eine Meldung, die sagt, was los ist, wenn hinter der Adresse niemand antwortet.
    /// </summary>
    [TestClass]
    public class ServerAdresseUnitTests
    {
        private const string Tunnel = "https://quizzer.example.org";

        [TestMethod]
        public void ATunnelAddressWithoutPortIsTakenAsItIs()
        {
            Assert.AreEqual("https://quizzer.example.org/api/v1/", ServerSession.NormalizeUrl(Tunnel).AbsoluteUri);
            Assert.AreEqual("https://quizzer.example.org/api/v1/", ServerSession.NormalizeUrl(" https://Quizzer.Example.org/ ").AbsoluteUri);
            Assert.AreEqual(443, ServerSession.NormalizeUrl(Tunnel).Port);
            Assert.ThrowsExactly<ArgumentException>(() => ServerSession.NormalizeUrl("http://quizzer.example.org"));
        }

        /// <summary>Ein oeffentliches Zertifikat geht ohne Fingerabdruck durch; ohne ihn gibt es aber keinen Ausweg bei Fehlern.</summary>
        [TestMethod]
        public void APublicCertificateNeedsNoFingerprint()
        {
            Assert.IsNull(QuizzerApiClient.NormalizeThumbprint(""), "ein leeres Feld ist kein Fingerabdruck");

            var callback = QuizzerApiClient.CreateHandler(QuizzerApiClient.NormalizeThumbprint("")).SslOptions.RemoteCertificateValidationCallback!;
            Assert.IsTrue(callback(this, null, null, SslPolicyErrors.None));
            Assert.IsFalse(callback(this, null, null, SslPolicyErrors.RemoteCertificateNameMismatch));
            Assert.IsFalse(callback(this, null, null, SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [TestMethod]
        public async Task TheProbeInTheSettingsAsksTheTunnelAddress()
        {
            var handler = new Antwort(_ => Json(HttpStatusCode.OK, new VersionResponse(ApiInfo.ApiVersion, 1, "test")));
            using var api = new QuizzerApiClient(ServerSession.NormalizeUrl(Tunnel), new MemoryTokenStore(), QuizzerApiClient.NormalizeThumbprint(""), handler);

            await api.GetVersionAsync();

            Assert.AreEqual("https://quizzer.example.org/api/v1/version", handler.Gefragt.Single());
            Assert.IsNull(api.PinnedCertificateSha256);
        }

        /// <summary>Echt gemessen: ein Port, auf dem niemand lauscht - wie ein Server, der aus ist.</summary>
        [TestMethod]
        public async Task ARefusedConnectionSaysTheServerIsDown()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            using var api = new QuizzerApiClient(new Uri($"https://127.0.0.1:{port}/api/v1/"), new MemoryTokenStore());
            var ex = await Assert.ThrowsExactlyAsync<ServerUnreachableException>(() => api.GetVersionAsync());

            StringAssert.StartsWith(ex.Message, QuizzerApiClient.ServerDown);
        }

        [TestMethod]
        public async Task AnUnknownNameSaysSoAndPointsAtTheAddress()
        {
            var ex = await UnreachableAsync(new HttpRequestException(HttpRequestError.NameResolutionError, "Der angegebene Host ist unbekannt.", new SocketException(11001)));

            StringAssert.StartsWith(ex.Message, QuizzerApiClient.Unreachable);
            StringAssert.Contains(ex.Message, "Adresse");
        }

        /// <summary>So kommt die Verbindungsfrist des Handlers an (gemessen M36 A6) - anders als eine langsame Antwort.</summary>
        [TestMethod]
        public async Task NoAnswerToConnectingSaysTheServerIsDownASlowAnswerDoesNot()
        {
            var keine = await UnreachableAsync(new TaskCanceledException("The operation was canceled.", new TimeoutException()));
            StringAssert.StartsWith(keine.Message, QuizzerApiClient.ServerDown);

            var langsam = await UnreachableAsync(new TaskCanceledException("The operation was canceled."));
            Assert.IsFalse(langsam.Message.StartsWith(QuizzerApiClient.Unreachable), langsam.Message);
        }

        /// <summary>Die bestehende Zertifikatsmeldung bleibt, wie sie war.</summary>
        [TestMethod]
        public async Task ACertificateProblemIsStillCalledThat()
        {
            var ex = await UnreachableAsync(new HttpRequestException(HttpRequestError.SecureConnectionError, "SSL", new AuthenticationException("Kette")));

            StringAssert.Contains(ex.Message, "Zertifikat");
            Assert.IsFalse(ex.Message.StartsWith(QuizzerApiClient.Unreachable), ex.Message);
        }

        /// <summary>
        /// Hinter dem Tunnel ist ein ausgeschalteter Rechner keine Verbindungsstoerung, sondern eine
        /// Fehlerseite (Cloudflare 530/1033). Eine eigene Meldung des Servers bleibt aber seine.
        /// </summary>
        [TestMethod]
        public async Task AnErrorPageFromTheTunnelSaysTheServerIsDown()
        {
            var seite = new Antwort(_ => new HttpResponseMessage((HttpStatusCode)530) { Content = new StringContent("error code: 1033", null, "text/plain") });
            using var api = new QuizzerApiClient(ServerSession.NormalizeUrl(Tunnel), new MemoryTokenStore(), handler: seite);
            var ex = await Assert.ThrowsExactlyAsync<ServerUnreachableException>(() => api.GetVersionAsync());
            StringAssert.StartsWith(ex.Message, QuizzerApiClient.ServerDown);
            StringAssert.Contains(ex.Message, "530");

            var eigene = new Antwort(_ => Json(HttpStatusCode.BadGateway, new ApiError("Die KI antwortet nicht.")));
            using var server = new QuizzerApiClient(ServerSession.NormalizeUrl(Tunnel), new MemoryTokenStore(), handler: eigene);
            var abgelehnt = await Assert.ThrowsExactlyAsync<ApiException>(() => server.GetVersionAsync());
            Assert.AreEqual("Die KI antwortet nicht.", abgelehnt.Message);
        }

        private static async Task<ServerUnreachableException> UnreachableAsync(Exception thrown)
        {
            using var api = new QuizzerApiClient(ServerSession.NormalizeUrl(Tunnel), new MemoryTokenStore(), handler: new Antwort(_ => throw thrown));
            return await Assert.ThrowsExactlyAsync<ServerUnreachableException>(() => api.GetVersionAsync());
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object body)
            => new(status) { Content = JsonContent.Create(body, body.GetType(), options: QuizzerApiClient.Json) };

        private sealed class Antwort(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
        {
            public List<string> Gefragt { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Gefragt.Add(request.RequestUri!.AbsoluteUri);
                return Task.FromResult(antwort(request));
            }
        }
    }
}
