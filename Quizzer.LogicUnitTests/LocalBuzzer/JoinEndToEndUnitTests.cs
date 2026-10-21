using Microsoft.AspNetCore.SignalR.Client;
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

        private sealed record Candidate(string CandidateId, string Name);

        private sealed record Created(Guid RequestId);

        private sealed record JoinState(string Status, string? AccessKey);

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

            var bertsKennung = candidates.Single(c => c.Name == "Bert").CandidateId;

            var response = await http.PostAsJsonAsync("/join/request", new { candidateId = bertsKennung, guestName = (string?)null });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var created = (await response.Content.ReadFromJsonAsync<Created>())!;

            var waiting = await http.GetFromJsonAsync<JoinState>($"/join/status/{created.RequestId}");
            Assert.AreEqual("pending", waiting!.Status);
            Assert.IsNull(waiting.AccessKey, "Vor dem Annehmen verraet der Server keinen Schluessel.");

            Assert.AreEqual(1, server.JoinRequests.Pending().Count, "Am Laptop steht die Anfrage.");
            server.JoinRequests.Accept(created.RequestId, bert.Id);

            var accepted = await http.GetFromJsonAsync<JoinState>($"/join/status/{created.RequestId}");
            Assert.AreEqual("accepted", accepted!.Status);
            Assert.AreEqual(server.AccessKeys.SchluesselFuer(bert.Id), accepted.AccessKey);
        }

        /// <summary>
        /// <b>Sicherheitsprüfung 2026-10-21:</b> was die Kandidatenliste herausgibt, öffnet den Hub
        /// nicht - weder als Kandidatenkennung noch als Spieler-Id. Vorher reichte die Id aus der
        /// Liste, um sich als Anna zu verbinden und sie zu verdrängen.
        /// </summary>
        [TestMethod]
        public async Task WhatTheCandidateListRevealsDoesNotOpenTheHub()
        {
            var candidates = (await http.GetFromJsonAsync<List<Candidate>>("/join/candidates"))!;
            var annasKennung = candidates.Single(c => c.Name == "Anna").CandidateId;

            Assert.IsFalse(annasKennung.Contains(anna.Id.ToString("N"), StringComparison.OrdinalIgnoreCase)
                    || annasKennung.Contains(anna.Id.ToString(), StringComparison.OrdinalIgnoreCase),
                "Die Kandidatenliste traegt die Spieler-Id.");

            foreach (var abfrage in new[] { $"key={annasKennung}", $"playerid={anna.Id}", $"key={anna.Id}" })
            {
                await using var verbindung = new HubConnectionBuilder()
                    .WithUrl($"http://127.0.0.1:{TestPort}/hub?{abfrage}")
                    .Build();

                var zugelassen = true;

                try
                {
                    await verbindung.StartAsync();
                    await Task.Delay(300);
                    zugelassen = verbindung.State == HubConnectionState.Connected;
                }
                catch
                {
                    zugelassen = false;
                }

                Assert.IsFalse(zugelassen, $"Mit '{abfrage}' liess der Hub ein fremdes Telefon als Anna hinein.");
            }

            // Gegenprobe: der echte Schluessel (QR-Code beim Spielleiter) oeffnet ihn.
            await using var echt = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={server.AccessKeys.SchluesselFuer(anna.Id)}")
                .Build();

            var zugewiesen = new TaskCompletionSource();
            echt.On<object>("Assigned", _ => zugewiesen.TrySetResult());

            await echt.StartAsync();

            Assert.AreEqual(zugewiesen.Task, await Task.WhenAny(zugewiesen.Task, Task.Delay(5000)),
                "Mit dem echten Schluessel wurde Anna nicht zugewiesen.");
        }

        [TestMethod]
        public async Task ANameThatIsNotOfferedIsRefused()
        {
            var response = await http.PostAsJsonAsync("/join/request", new { candidateId = "erfunden-erfunden-1", guestName = (string?)null });

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
