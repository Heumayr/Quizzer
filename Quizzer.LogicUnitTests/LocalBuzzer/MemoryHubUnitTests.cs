using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.TestShared;
using System.Text.Json;
using static Quizzer.TestShared.TestNetwork;
using static Quizzer.LogicUnitTests.LocalBuzzer.HubTest;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Das Memory ueber den echten Hub (M35 C5), zwei Telefone als SignalR-Clients: wer umdreht, sagt die
    /// Verbindung; nur das umdrehende Telefon erfaehrt seine Karte; wer fertig ist, ist gesperrt; der Server
    /// schliesst an der Frist selbst; und das andere Telefon sieht nie ein Motiv.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MemoryHubUnitTests
    {
        private const int TestPort = TestPorts.MemoryHub;
        private const int Paare = 6;
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        /// <summary>Ein Telefon: nur die Zustaende aus <c>StateChanged</c> zaehlen, <c>Assigned</c> eigens.</summary>
        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende, TaskCompletionSource<ClientLayoutStateDto> Angemeldet);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
        private readonly StellbareUhr uhr = new();
        private readonly List<MinigameSubmission> abgaben = new();
        private readonly List<MinigameRoundClosed> geschlossen = new();
        private readonly List<HubConnection> offen = new();

        [TestInitialize]
        public async Task StartServer()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };
            game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };

            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            server = new BuzzerServer { GetGame = () => game };
            await HubTest.StartAsync(server, TestPort);
            server.BuzzerController!.EventBus.MinigameSubmitted += a => { lock (abgaben) abgaben.Add(a); };
            server.BuzzerController.EventBus.MinigameRoundClosed += r => { lock (geschlossen) geschlossen.Add(r); };

            // Paar k liegt auf den Karten 2k und 2k+1 (MemoryBelegung.Mische, Grundordnung).
            var spiel = server.BuzzerController.StateManager.MinigameState;
            spiel.Uhr = uhr;
            spiel.Zufall = (von, bis) => bis == MemoryBelegung.Motive.Count ? von : bis - 1;
        }

        [TestCleanup]
        public async Task StopServer()
        {
            foreach (var v in offen)
                await v.DisposeAsync();

            if (server != null)
                await server.DisposeAsync();
        }

        private async Task<Telefon> VerbindeAsync(Player player)
        {
            var telefon = new Telefon(
                new HubConnectionBuilder()
                    .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={Uri.EscapeDataString(server.AccessKeys.SchluesselFuer(player.Id))}")
                    .Build(),
                new List<ClientLayoutStateDto>(),
                new TaskCompletionSource<ClientLayoutStateDto>(TaskCreationOptions.RunContinuationsAsynchronously));

            telefon.Verbindung.On<ClientLayoutStateDto>("StateChanged", dto => { lock (telefon.Zustaende) telefon.Zustaende.Add(dto); });
            telefon.Verbindung.On<ClientLayoutStateDto>("Assigned", dto => telefon.Angemeldet.TrySetResult(dto));
            await telefon.Verbindung.StartAsync();
            offen.Add(telefon.Verbindung);

            // Erst mit "Assigned" ist die Verbindung am Server eingetragen (quizzer-testaufbau.md).
            Assert.IsTrue(await WarteAsync(() => telefon.Angemeldet.Task.IsCompleted), $"{player.Designation} bekam keine Zuordnung.");
            return telefon;
        }

        /// <summary>Ein <c>playerId</c> in der Nachricht gibt sich als jemand anderes aus - der Server darf es nicht glauben.</summary>
        private async Task DreheAsync(Telefon telefon, int karte, Guid? alsWer = null)
        {
            uhr.Vor(TimeSpan.FromMilliseconds(250));
            await telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.Memory,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                playerId = alsWer,
                payload = new { card = karte, playerId = alsWer },
            });
        }

        private static List<int> Indizes(JsonElement karten) => karten.EnumerateArray().Select(k => k.GetProperty("index").GetInt32()).ToList();

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.ToList(); }

        private int Abgaben() { lock (abgaben) return abgaben.Count; }

        private async Task StarteAsync()
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(
                MinigameKind.Memory, "Memory", "Regel", 1, DurationSeconds: 60));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        [TestMethod]
        public async Task OnlyTheFlippingPhoneLearnsItsCardsAndTheServerDecidesTheWinner()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync();

            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Running)), "Die Runde kommt nicht an.");
            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1) - TimeSpan.FromMilliseconds(250));
            var bertVorher = Alle(b).Count;

            await DreheAsync(a, 0);
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Indizes(Art(z).GetProperty("openCards")).SequenceEqual([0])),
                "Anna erfaehrt ihre Karte nicht.");
            Assert.AreEqual(MemoryBelegung.Motive[0].Symbol, Art(Letzter(a)!).GetProperty("openCards")[0].GetProperty("symbol").GetString());

            for (var karte = 1; karte < 2 * Paare; karte++)
                await DreheAsync(a, karte);

            Assert.IsTrue(await WarteAsync(() => Abgaben() == 2 * Paare), "Die Zuege erreichen den Spielleiter nicht.");
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Info(z).GetProperty("ownSubmitted").GetBoolean()),
                "Anna ist fertig und darf weiter umdrehen.");
            Assert.AreEqual(1000 + 11 * 250, Art(Letzter(a)!).GetProperty("ownFinishedMs").GetInt32(), "Die Zeit kommt nicht von der Serveruhr.");
            Assert.AreEqual(MinigamePhase.Running, server.BuzzerController!.StateManager.MinigameState.Phase, "Die Runde schliesst, obwohl Bert noch spielt.");

            await Task.Delay(150);
            Assert.AreEqual(bertVorher, Alle(b).Count, "Bert bekam Zustaende aus Annas Zuegen.");

            await DreheAsync(b, 4, alsWer: anna.Id);
            Assert.IsTrue(await WarteAsync(() => Abgaben() == 2 * Paare + 1));
            lock (abgaben)
            {
                Assert.AreEqual(bert.Id, abgaben[^1].Player.Id, "Der Server glaubt dem playerId in der Nachricht statt der Verbindung.");
                Assert.AreEqual(new MemoryZug(4, false, new MemoryStand(0, null)), abgaben[^1].Value);
            }

            uhr.Vor(TimeSpan.FromSeconds(60));

            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)),
                "Nach der Frist schliesst der Server die Runde nicht selbst.");
            lock (geschlossen)
                Assert.AreEqual(1, geschlossen.Count, "Der Spielleiter erfaehrt das Ende nicht genau einmal.");
            Assert.IsTrue(Art(Letzter(a)!).GetProperty("isWinner").GetBoolean(), "Wer fertig ist, hat nicht gewonnen.");
            Assert.IsFalse(Art(Letzter(b)!).GetProperty("isWinner").GetBoolean());

            await DreheAsync(b, 5);
            await Task.Delay(200);
            Assert.AreEqual(2 * Paare + 1, Abgaben(), "Ein Umdrehen nach der Frist wurde angenommen.");

            // Bert hat nur Karte 4 gesehen (Paar 2) - kein anderes Motiv in irgendeinem seiner Zustaende.
            foreach (var dto in Alle(b))
            {
                var inhalt = Zustandsinhalt.Von(dto.LayoutInfo);
                for (var paar = 0; paar < Paare; paar++)
                {
                    if (paar != 2)
                        Assert.IsFalse(inhalt.EnthaeltText(MemoryBelegung.Motive[paar].Symbol), $"Bert kennt das Motiv {MemoryBelegung.Motive[paar].Name}.");
                }
            }
        }

        [TestMethod]
        public async Task AReconnectingPhoneGetsItsBoardBack()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await StarteAsync();

            uhr.Vor(Vorlauf);
            foreach (var karte in new[] { 4, 5, 8 })
                await DreheAsync(a, karte);
            Assert.IsTrue(await WarteAsync(() => Abgaben() == 3));

            await a.Verbindung.StopAsync();
            Assert.IsTrue(await WarteAsync(() => anna.ConnectionState != PlayerConnection.Connected));

            var wieder = await VerbindeAsync(anna);

            // Was ein wiederkommendes Telefon bekommt, ist der volle Zustand in "Assigned".
            var zugeordnet = Art(await wieder.Angemeldet.Task);
            CollectionAssert.AreEqual(new[] { 4, 5 }, Indizes(zugeordnet.GetProperty("foundCards")), "Die Paare sind nach dem Wiederverbinden weg.");
            CollectionAssert.AreEqual(new[] { 8 }, Indizes(zugeordnet.GetProperty("openCards")), "Die offene Karte ist nach dem Wiederverbinden weg.");
            Assert.AreEqual(MinigamePhase.Running, server.BuzzerController!.StateManager.MinigameState.Phase,
                "Die Trennung hat die Runde geschlossen.");
        }
    }
}
