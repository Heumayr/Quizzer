using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.TestShared;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Der Hub löst eine Runde genau einmal auf (M37 P6, R2-4), auch wenn die letzten Abgaben zugleich kommen: er fragt den
    /// Zustand, ob diese Abgabe geschlossen hat, statt danach <c>Locked</c> zu lesen. Gemessen vor der Behebung: zwei letzte
    /// Tipps zugleich lösten fast immer doppelt aus - zwischen Abgabe und Lesen schickt der Hub allen ihren Stand.
    /// </summary>
    [TestClass]
    public class RundenschlussHubUnitTests
    {
        private const int TestPort = TestPorts.RundenschlussHub;
        private const int Runden = 15;

        private BuzzerServer server = null!;
        private Player anna = null!, bert = null!, carl = null!;
        private readonly List<HubConnection> telefone = new();

        [TestInitialize]
        public async Task StartServer()
        {
            (anna, bert, carl) = (Spieler("Anna"), Spieler("Bert"), Spieler("Carl"));

            var game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };
            foreach (var p in new[] { anna, bert, carl })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            server = new BuzzerServer { GetGame = () => game };
            await HubTest.StartAsync(server, TestPort);
        }

        [TestCleanup]
        public async Task StopServer()
        {
            foreach (var t in telefone)
            {
                try { await t.DisposeAsync(); } catch { }
            }

            if (server != null)
                await server.DisposeAsync();
        }

        private static Player Spieler(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        private BuzzerController Steuerung => server.BuzzerController!;

        private async Task<HubConnection> VerbindeAsync(Player spieler)
        {
            var verbindung = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={server.AccessKeys.SchluesselFuer(spieler.Id)}")
                .Build();
            var zugeordnet = 0;

            verbindung.On<ClientLayoutStateDto>("Assigned", _ => Interlocked.Exchange(ref zugeordnet, 1));
            await verbindung.StartAsync();
            telefone.Add(verbindung);

            Assert.IsTrue(await WarteAsync(() => Volatile.Read(ref zugeordnet) == 1), $"{spieler.Designation} bekam keine Zuordnung.");
            return verbindung;
        }

        [TestMethod]
        public async Task TwoLastInputsAtOnceResolveTheRoundOnce()
        {
            var (a, b, c) = (await VerbindeAsync(anna), await VerbindeAsync(bert), await VerbindeAsync(carl));
            var aufgeloest = 0;
            Steuerung.EventBus.AllPlayersSubmittedInput += (_, _) => Interlocked.Increment(ref aufgeloest);
            Steuerung.StateManager.BuzzerInputState.Infos = new BuzzerInputState.BuzzerInputInfo { InputType = "number" };

            for (var runde = 1; runde <= Runden; runde++)
            {
                await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);
                await a.InvokeAsync("SubmitInput", new { value = "1" });

                await Task.WhenAll(b.InvokeAsync("SubmitInput", new { value = "2" }), c.InvokeAsync("SubmitInput", new { value = "3" }));

                Assert.IsTrue(await WarteAsync(() => Volatile.Read(ref aufgeloest) >= runde), $"Runde {runde} wurde nicht aufgelöst.");
            }

            await Task.Delay(200);
            Assert.AreEqual(Runden, Volatile.Read(ref aufgeloest), "Zwei letzte Tipps zugleich lösten die Runde doppelt auf.");
        }

        [TestMethod]
        public async Task UmstimmenOrTwoLastVotesAtOnceResolveTheRoundOnce()
        {
            var (a, b, c) = (await VerbindeAsync(anna), await VerbindeAsync(bert), await VerbindeAsync(carl));
            var aufgeloest = 0;
            Steuerung.EventBus.AllPlayersSelectedKeys += (_, _) => Interlocked.Increment(ref aufgeloest);

            // Aufdecker: ein langsamer Abnehmer weitet das Fenster zwischen Abgabe und Auflösen, wie beim Tipp das Senden.
            Steuerung.EventBus.PlayerSelectedKeys += (_, _) => Thread.Sleep(5);
            Steuerung.StateManager.BuzzerKeySelector.Infos = new BuzzerKeySelector.BuzzerKeySelectorInfo
            {
                KeysAndDesignations = new() { ["A"] = "Rom", ["B"] = "Paris", ["C"] = "Wien" },
            };

            for (var runde = 1; runde <= Runden; runde++)
            {
                await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);
                await a.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" } });

                // Ungerade Runden: Anna stimmt um, während Carl als Letzter abstimmt; gerade: Bert und Carl zugleich als Letzte.
                if (runde % 2 == 1)
                    await b.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" } });

                await Task.WhenAll(
                    c.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "B" } }),
                    (runde % 2 == 1 ? a : b).InvokeAsync("SelectionResults", new { selectedKeys = new[] { "C" } }));

                Assert.IsTrue(await WarteAsync(() => Volatile.Read(ref aufgeloest) >= runde), $"Runde {runde} wurde nicht aufgelöst.");
            }

            await Task.Delay(200);
            Assert.AreEqual(Runden, Volatile.Read(ref aufgeloest), "Gleichzeitige letzte Stimmen lösten die Runde doppelt auf.");
        }
    }
}
