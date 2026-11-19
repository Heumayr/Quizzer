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
    /// Die Runde, für die eine Abgabe gilt (Durchsicht M37, P11). P10-3: kommt „Runde zurücksetzen“, nachdem die letzte Abgabe
    /// die Runde geschlossen hat, aber bevor der Hub sie auflöst, bleibt die neue Runde offen und wird nicht leer aufgelöst.
    /// P10-4: eine Abgabe, die das Telefon für eine schon zurückgesetzte Runde schickt, zählt nicht in der neuen.
    /// Deterministisch: das Zurücksetzen läuft im Abnehmer der Einzelabgabe am Ereignisbus, also mitten im Fenster.
    /// </summary>
    [TestClass]
    public class RundenmarkeHubUnitTests
    {
        private const int TestPort = TestPorts.RundenmarkeHub;

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

            Steuerung.StateManager.BuzzerInputState.Infos = new BuzzerInputState.BuzzerInputInfo { InputType = "number" };
            Steuerung.StateManager.BuzzerKeySelector.Infos = new BuzzerKeySelector.BuzzerKeySelectorInfo
            {
                KeysAndDesignations = new() { ["A"] = "Rom", ["B"] = "Paris", ["C"] = "Wien" },
            };
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

        /// <summary>Die Spielleitung setzt zurück - wie aus dem Fragefenster, auf einem eigenen Faden, und wartet, bis es durch ist.</summary>
        private void SetzeZurueck(BuzzerControlsLayout layout) => Task.Run(() => Steuerung.ResetRoundAsync(1, layout)).GetAwaiter().GetResult();

        [TestMethod]
        public async Task AResetWhileTheLastInputIsResolvedLeavesTheNewRoundOpen()
        {
            var (a, b, c) = (await VerbindeAsync(anna), await VerbindeAsync(bert), await VerbindeAsync(carl));
            var aufgeloest = new List<int>();
            var zurueckgesetzt = 0;

            Steuerung.EventBus.AllPlayersSubmittedInput += (alle, _) => { lock (aufgeloest) aufgeloest.Add(alle.Count); };
            Steuerung.EventBus.PlayerSubmittedInput += (tipp, _) =>
            {
                if (tipp.PlayerId == carl.Id && Interlocked.Exchange(ref zurueckgesetzt, 1) == 0)
                    SetzeZurueck(BuzzerControlsLayout.Input);
            };

            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);
            await a.InvokeAsync("SubmitInput", new { value = "1" });
            await b.InvokeAsync("SubmitInput", new { value = "2" });
            await c.InvokeAsync("SubmitInput", new { value = "3" });

            Assert.AreEqual(1, Volatile.Read(ref zurueckgesetzt), "Carls Tipp kam nicht an - dann misst der Test nichts.");
            await Task.Delay(100);

            var zustand = Steuerung.StateManager;
            Assert.IsFalse(zustand.BuzzerInputState.Locked || zustand.AllLocked, "Die neue Runde ist nach dem Zurücksetzen geschlossen.");
            lock (aufgeloest)
                Assert.AreEqual(0, aufgeloest.Count, $"Nach dem Zurücksetzen wurde aufgelöst ({string.Join(", ", aufgeloest)} Tipps).");
        }

        [TestMethod]
        public async Task AResetWhileTheLastVoteIsResolvedLeavesTheNewRoundOpen()
        {
            var (a, b, c) = (await VerbindeAsync(anna), await VerbindeAsync(bert), await VerbindeAsync(carl));
            var aufgeloest = new List<int>();
            var zurueckgesetzt = 0;

            Steuerung.EventBus.AllPlayersSelectedKeys += (alle, _) => { lock (aufgeloest) aufgeloest.Add(alle.Count); };
            Steuerung.EventBus.PlayerSelectedKeys += (stimme, _) =>
            {
                if (stimme.PlayerId == carl.Id && Interlocked.Exchange(ref zurueckgesetzt, 1) == 0)
                    SetzeZurueck(BuzzerControlsLayout.KeySelect);
            };

            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);
            await a.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" } });
            await b.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "B" } });
            await c.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "C" } });

            Assert.AreEqual(1, Volatile.Read(ref zurueckgesetzt), "Carls Stimme kam nicht an - dann misst der Test nichts.");
            await Task.Delay(100);

            var zustand = Steuerung.StateManager;
            Assert.IsFalse(zustand.BuzzerKeySelector.Locked || zustand.AllLocked, "Die neue Runde ist nach dem Zurücksetzen geschlossen.");
            lock (aufgeloest)
                Assert.AreEqual(0, aufgeloest.Count, $"Nach dem Zurücksetzen wurde aufgelöst ({string.Join(", ", aufgeloest)} Stimmen).");
        }

        [TestMethod]
        public async Task AnInputForAResetRoundDoesNotCountInTheNewOne()
        {
            var a = await VerbindeAsync(anna);
            var angekommen = new List<string>();
            Steuerung.EventBus.PlayerSubmittedInput += (tipp, marke) => { lock (angekommen) angekommen.Add($"{tipp.Value}@{marke}"); };

            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);
            var gesehen = Steuerung.StateManager.ResetCount;
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);

            await a.InvokeAsync("SubmitInput", new { value = "41", resetCount = gesehen });
            Assert.IsFalse(Steuerung.StateManager.BuzzerInputState.InputsForPlayer.ContainsKey(anna.Id), "Der Tipp für die alte Runde zählt in der neuen.");

            await a.InvokeAsync("SubmitInput", new { value = "42", resetCount = Steuerung.StateManager.ResetCount });
            Assert.AreEqual("42", Steuerung.StateManager.BuzzerInputState.InputsForPlayer[anna.Id].Value, "Der Tipp für die laufende Runde kam nicht an.");
            lock (angekommen)
                CollectionAssert.AreEqual(new[] { $"42@{Steuerung.StateManager.ResetCount}" }, angekommen,
                    "Die Spielleitung hörte vom Tipp der alten Runde - oder ohne die Marke der Runde, in der er zählt (P12-1).");
        }

        [TestMethod]
        public async Task AVoteForAResetRoundDoesNotCountInTheNewOne()
        {
            var a = await VerbindeAsync(anna);
            var angekommen = new List<string>();
            Steuerung.EventBus.PlayerSelectedKeys += (stimme, marke) => { lock (angekommen) angekommen.Add($"{string.Join("", stimme.SelectedKeys)}@{marke}"); };

            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);
            var gesehen = Steuerung.StateManager.ResetCount;
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);

            await a.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" }, resetCount = gesehen });
            Assert.IsFalse(Steuerung.StateManager.BuzzerKeySelector.KeyResultsForPlayer.ContainsKey(anna.Id), "Die Stimme für die alte Runde zählt in der neuen.");

            await a.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "B" }, resetCount = Steuerung.StateManager.ResetCount });
            CollectionAssert.AreEqual(new[] { "B" }, Steuerung.StateManager.BuzzerKeySelector.KeyResultsForPlayer[anna.Id].SelectedKeys, "Die Stimme für die laufende Runde kam nicht an.");
            lock (angekommen)
                CollectionAssert.AreEqual(new[] { $"B@{Steuerung.StateManager.ResetCount}" }, angekommen,
                    "Die Spielleitung hörte von der Stimme der alten Runde - oder ohne die Marke der Runde, in der sie zählt (P12-1).");
        }
    }
}
