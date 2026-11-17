using Microsoft.AspNetCore.Http;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Server.Infrastructure;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Versionsriegel (M36 A4): ein WPF mit anderem Abgleichformat haelt schon an der Anmeldung,
    /// mit einer Meldung, die auch ein altes Programm anzeigt.
    /// </summary>
    [TestClass]
    public sealed class ClientFormatGateTests
    {
        private static string Format(int wert) => wert.ToString(CultureInfo.InvariantCulture);

        private static async Task<HttpResponseMessage> LoginWithFormatAsync(HttpClient client, string? format)
        {
            if (format != null)
            {
                client.DefaultRequestHeaders.Remove(ApiInfo.SyncFormatHeader);
                client.DefaultRequestHeaders.Add(ApiInfo.SyncFormatHeader, format);
            }

            return await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest(ServerTestHost.AdminUser, ServerTestHost.AdminPassword, ServerTestHost.NextAdminCode()));
        }

        [TestMethod]
        public async Task AProgramWithTheCurrentFormatGetsThrough()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(SyncSnapshot.CurrentFormatVersion));

            Assert.AreEqual(HttpStatusCode.OK, antwort.StatusCode);
        }

        [TestMethod]
        public async Task AnOlderProgramIsStoppedAtLoginWithAMessageItCanShow()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(SyncSnapshot.CurrentFormatVersion - 1));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
            Assert.AreEqual(ClientFormatGate.TooOld, (await antwort.Content.ReadFromJsonAsync<ApiError>())?.Message);
        }

        /// <summary>M35 C9: ein WPF ohne Rohwert und Klaus am Ergebnis setzte beim Push still null und 0 ein.</summary>
        [TestMethod]
        public async Task AProgramWithoutTheLeaderboardColumnsIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(23));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>M37 E06: ein WPF ohne den Zeitpunkt an der Zelle setzte beim Push still null ein.</summary>
        [TestMethod]
        public async Task AProgramWithoutThePlayedTimeIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(24));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>M37 T1: ein WPF ohne Teams kennt GameTeam nicht und nahm beim Push still jede Zuteilung heraus.</summary>
        [TestMethod]
        public async Task AProgramWithoutTeamsIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(25));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>M37 T5: ein WPF ohne Teamberatung spielte die Zelle ohne Teamregel und schrieb volle Feldpunkte.</summary>
        [TestMethod]
        public async Task AProgramWithoutTheTeamCouncilIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(26));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>M37 E4: ein WPF ohne Finale spielte die Zelle ohne Einsatz und schrieb die Feldpunkte statt der Einsätze.</summary>
        [TestMethod]
        public async Task AProgramWithoutTheFinalIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(27));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>M37 T6: ein WPF ohne „Gleich gedacht" und „Alle oder keiner" spielte die Zelle ohne Teamregel und schrieb volle Feldpunkte.</summary>
        [TestMethod]
        public async Task AProgramWithoutSameThoughtAndAllOrNothingIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(28));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>M37 T7: ein WPF ohne „Team-Abstimmung" spielte die Zelle ohne Teamregel und schrieb je Mitglied die eigene Wertung.</summary>
        [TestMethod]
        public async Task AProgramWithoutTeamVoteIsStopped()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(29));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        [TestMethod]
        public async Task ANewerProgramIsStoppedWithItsOwnMessage()
        {
            await using var factory = ServerTestHost.CreateFactory();

            var antwort = await LoginWithFormatAsync(factory.CreateClient(), Format(SyncSnapshot.CurrentFormatVersion + 1));

            Assert.AreEqual(HttpStatusCode.Conflict, antwort.StatusCode);
            Assert.AreEqual(ClientFormatGate.TooNew, (await antwort.Content.ReadFromJsonAsync<ApiError>())?.Message);
        }

        /// <summary>Die Versionsabfrage muss jedes Programm erreichen, sonst kann es nicht einmal fragen.</summary>
        [TestMethod]
        public async Task TheVersionQueryStaysOpenForEveryProgram()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Remove(ApiInfo.SyncFormatHeader);
            client.DefaultRequestHeaders.Add(ApiInfo.SyncFormatHeader, Format(SyncSnapshot.CurrentFormatVersion - 1));

            Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/version")).StatusCode);
        }

        /// <summary>Seit Format 2 ist ein Programm ohne Kopf zu alt - so sehen die WPF von vor dem Riegel aus.</summary>
        [TestMethod]
        public async Task AProgramWithoutTheHeaderIsAnOldProgram()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Remove(ApiInfo.SyncFormatHeader);

            var antwort = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            Assert.AreEqual(HttpStatusCode.UpgradeRequired, antwort.StatusCode);
        }

        /// <summary>Die WPF von vor dem Riegel schicken keinen Kopf; sie sprechen Format 1.</summary>
        [TestMethod]
        public void AProgramWithoutTheHeaderCountsAsFormat1()
        {
            Assert.AreEqual(ClientFormatGate.Check("1"), ClientFormatGate.Check(null));
            Assert.AreEqual(ClientFormatGate.Check("1"), ClientFormatGate.Check(""));
        }

        [TestMethod]
        [DataRow("zwei")]
        [DataRow("-1")]
        [DataRow("1.0")]
        [DataRow(" 1")]
        public void AnUnreadableHeaderIsRejected(string wert)
        {
            Assert.AreEqual(StatusCodes.Status400BadRequest, ClientFormatGate.Check(wert)?.Status);
        }

        [TestMethod]
        public async Task TheClientNamesItsFormatOnEveryRequest()
        {
            var handler = new Aufzeichner(HttpStatusCode.OK, """{"apiVersion":1,"syncFormatVersion":1,"serverVersion":"t"}""");
            using var api = new QuizzerApiClient(new Uri("https://quizzer.test"), new MemoryTokenStore(), handler: handler);

            await api.GetVersionAsync();

            Assert.AreEqual(Format(SyncSnapshot.CurrentFormatVersion), handler.Format);
        }

        /// <summary>Was der Server dem alten Programm sagt, kommt beim Nutzer an - auch ohne Rumpf.</summary>
        [TestMethod]
        public async Task TheServersWordsReachTheUser()
        {
            var mitRumpf = new Aufzeichner(HttpStatusCode.UpgradeRequired, $$"""{"message":"{{ClientFormatGate.TooOld}}"}""");
            var ohneRumpf = new Aufzeichner(HttpStatusCode.UpgradeRequired, "");
            using var a = new QuizzerApiClient(new Uri("https://quizzer.test"), new MemoryTokenStore(), handler: mitRumpf);
            using var b = new QuizzerApiClient(new Uri("https://quizzer.test"), new MemoryTokenStore(), handler: ohneRumpf);

            var ausA = await Assert.ThrowsExactlyAsync<ApiException>(() => a.GetVersionAsync());
            var ausB = await Assert.ThrowsExactlyAsync<ApiException>(() => b.GetVersionAsync());

            Assert.AreEqual(ClientFormatGate.TooOld, ausA.Message);
            StringAssert.Contains(ausB.Message, "Bitte Quizzer aktualisieren");
        }

        private sealed class Aufzeichner(HttpStatusCode status, string rumpf) : HttpMessageHandler
        {
            public string? Format { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Format = request.Headers.TryGetValues(ApiInfo.SyncFormatHeader, out var werte) ? string.Join(",", werte) : null;
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(rumpf, Encoding.UTF8, "application/json"),
                });
            }
        }
    }
}
