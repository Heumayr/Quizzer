using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// M10 am echten Buzzer-Server: Name holen, Anfrage stellen, der Spielleiter nimmt an, das
    /// Telefon erfaehrt seine Kennung. Dieselben Wege, die join.js geht.
    /// </summary>
    [TestClass]
    public sealed class JoinEndToEndUnitTests
    {
        private const int TestPort = 5397;

        private BuzzerServer server = null!;
        private HttpClient http = null!;
        private readonly Player anna = new() { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
        private readonly Player bert = new() { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };

        private sealed record Candidate(Guid PlayerId, string Name);

        private sealed record Created(Guid RequestId);

        private sealed record JoinState(string Status, Guid? PlayerId);

        [TestInitialize]
        public async Task StartServer()
        {
            var game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };
            game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = anna.Id, Player = anna });

            server = new BuzzerServer
            {
                GetGame = () => game,
                // Bert spielt noch nicht mit, darf sich aber melden.
                GetJoinCandidates = () => [new JoinCandidate(anna.Id, "Anna"), new JoinCandidate(bert.Id, "Bert")],
            };

            await server.StartAsync(new BuzzerServerOptions { Port = TestPort, BindAddress = "127.0.0.1" });
            Assert.AreEqual(ServerState.Running, server.ServerState);

            http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{TestPort}") };
        }

        [TestCleanup]
        public async Task StopServer()
        {
            http?.Dispose();
            if (server != null)
                await server.DisposeAsync();
        }

        [TestMethod]
        public async Task APhoneJoinsOnlyAfterTheGameMasterAccepts()
        {
            var candidates = await http.GetFromJsonAsync<List<Candidate>>("/join/candidates");
            CollectionAssert.AreEquivalent(new[] { "Anna", "Bert" }, candidates!.Select(c => c.Name).ToList());

            var response = await http.PostAsJsonAsync("/join/request", new { playerId = bert.Id, guestName = (string?)null });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var created = (await response.Content.ReadFromJsonAsync<Created>())!;

            var waiting = await http.GetFromJsonAsync<JoinState>($"/join/status/{created.RequestId}");
            Assert.AreEqual("pending", waiting!.Status);
            Assert.IsNull(waiting.PlayerId, "Vor dem Annehmen verraet der Server keine Kennung.");

            Assert.AreEqual(1, server.JoinRequests.Pending().Count, "Am Laptop steht die Anfrage.");
            server.JoinRequests.Accept(created.RequestId, bert.Id);

            var accepted = await http.GetFromJsonAsync<JoinState>($"/join/status/{created.RequestId}");
            Assert.AreEqual("accepted", accepted!.Status);
            Assert.AreEqual(bert.Id, accepted.PlayerId);
        }

        [TestMethod]
        public async Task ANameThatIsNotOfferedIsRefused()
        {
            var response = await http.PostAsJsonAsync("/join/request", new { playerId = Guid.NewGuid(), guestName = (string?)null });

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, server.JoinRequests.Pending().Count);
        }

        [TestMethod]
        public async Task AnUnknownRequestIdIsNotFound()
        {
            var response = await http.GetAsync($"/join/status/{Guid.NewGuid()}");

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
