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
    /// Der Zocker ueber den echten Hub (M35 C4), drei Telefone als SignalR-Clients: der Server schliesst die
    /// Runde am Absturz selbst (Fristwache), ein spaeter Ausstieg zaehlt nicht, und kein Telefon bekommt den
    /// Absturzpunkt, solange die Runde laeuft.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ZockerHubUnitTests
    {
        private const int TestPort = TestPorts.ZockerHub;

        /// <summary>Wurf fuer den Absturz bei ×2,40.</summary>
        private const int Wurf240 = 5834;

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private readonly StellbareUhr uhr = new();
        private readonly List<MinigameSubmission> abgaben = new();
        private readonly List<MinigameRoundClosed> geschlossen = new();
        private readonly List<HubConnection> offen = new();

        [TestInitialize]
        public async Task StartServer()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };
            carl = new Player { Id = Guid.NewGuid(), Designation = "Carl", DisplayName = "Carl" };
            game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };

            foreach (var p in new[] { anna, bert, carl })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            server = new BuzzerServer { GetGame = () => game };
            await HubTest.StartAsync(server, TestPort);
            server.BuzzerController!.EventBus.MinigameSubmitted += a => { lock (abgaben) abgaben.Add(a); };
            server.BuzzerController.EventBus.MinigameRoundClosed += r => { lock (geschlossen) geschlossen.Add(r); };

            var spiel = server.BuzzerController.StateManager.MinigameState;
            spiel.Uhr = uhr;
            spiel.Zufall = (_, _) => Wurf240;
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
                new List<ClientLayoutStateDto>());

            telefon.Verbindung.On<ClientLayoutStateDto>("StateChanged", dto => { lock (telefon.Zustaende) telefon.Zustaende.Add(dto); });
            telefon.Verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (telefon.Zustaende) telefon.Zustaende.Add(dto); });
            await telefon.Verbindung.StartAsync();
            offen.Add(telefon.Verbindung);

            Assert.IsTrue(await WarteAsync(() => player.ConnectionState == PlayerConnection.Connected), $"{player.Designation} ist nicht verbunden.");
            return telefon;
        }

        /// <summary>Ein <c>playerId</c> in der Nachricht gibt sich als jemand anderes aus - der Server darf es nicht glauben.</summary>
        private Task SteigAusAsync(Telefon telefon, Guid? alsWer = null)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.Crash,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                playerId = alsWer,
                payload = new { cashOut = true, playerId = alsWer },
            });

        /// <summary>Die Phase des Minispiels; -1 fuer einen Zustand ohne Minispiel (vor dem Umschalten).</summary>
        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.ToList(); }

        private int Abgaben() { lock (abgaben) return abgaben.Count; }

        private async Task StarteAsync()
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.Crash, "Zocker", "Regel", 1));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        [TestMethod]
        public async Task TheServerClosesAtTheCrashAndALateCashOutDoesNotCount()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await StarteAsync();

            uhr.Vor(TimeSpan.FromSeconds(3 + 2));
            await SteigAusAsync(a);
            await SteigAusAsync(b, alsWer: anna.Id);

            Assert.IsTrue(await WarteAsync(() => Abgaben() == 2), "Die Ausstiege erreichen den Spielleiter nicht.");
            lock (abgaben)
            {
                CollectionAssert.AreEquivalent(new[] { anna.Id, bert.Id }, abgaben.Select(x => x.Player.Id).ToList(),
                    "Der Server glaubt dem playerId in der Nachricht statt der Verbindung.");
                Assert.IsTrue(abgaben.All(x => x.Value is ZockerAusstieg { Multiplikator: 128 }), "Der Multiplikator kommt nicht von der Serveruhr.");
            }

            await Task.Delay(3 * 50);
            Assert.AreEqual(MinigamePhase.Running, server.BuzzerController!.StateManager.MinigameState.Phase, "Die Runde schliesst vor dem Absturz.");

            uhr.Vor(ZockerEvaluator.Absturzzeit(240));

            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)),
                "Nach dem Absturz schliesst der Server die Runde nicht selbst.");
            lock (geschlossen)
                Assert.AreEqual(1, geschlossen.Count, "Der Spielleiter erfaehrt den Absturz nicht genau einmal.");

            var carlAmEnde = Art(Letzter(c)!);
            Assert.IsTrue(carlAmEnde.GetProperty("crashed").GetBoolean());
            Assert.AreEqual(240, carlAmEnde.GetProperty("crashMultiplier").GetInt32());
            Assert.AreEqual(JsonValueKind.Null, carlAmEnde.GetProperty("ownMultiplier").ValueKind);

            await SteigAusAsync(c);
            await Task.Delay(200);
            Assert.AreEqual(2, Abgaben(), "Ein Ausstieg nach dem Absturz wurde angenommen.");

            // Geheim, solange die Runde lief: in keinem Zustand irgendeines Telefons.
            foreach (var t in new[] { a, b, c })
            {
                foreach (var dto in Alle(t).Where(z => Phase(z) == (int)MinigamePhase.Running))
                    Assert.AreEqual(JsonValueKind.Null, Art(dto).GetProperty("crashMultiplier").ValueKind, "Ein Telefon kennt den Absturzpunkt vor dem Absturz.");
            }

            Assert.IsTrue(Alle(c).Any(z => Phase(z) == (int)MinigamePhase.Running), "Carl hat die laufende Runde nie gesehen - die Probe misst nichts.");
        }

        [TestMethod]
        public async Task AReconnectingPhoneGetsItsCashOutBack()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await StarteAsync();

            uhr.Vor(TimeSpan.FromSeconds(3 + 2));
            await SteigAusAsync(a);
            Assert.IsTrue(await WarteAsync(() => Abgaben() == 1));

            await a.Verbindung.StopAsync();
            Assert.IsTrue(await WarteAsync(() => anna.ConnectionState != PlayerConnection.Connected));

            var wieder = await VerbindeAsync(anna);

            Assert.IsTrue(await WarteAsync(() => Letzter(wieder) is { } z && Info(z).GetProperty("ownSubmitted").GetBoolean()),
                "Das wiederverbundene Telefon weiss nicht, dass es ausgestiegen ist.");
            Assert.AreEqual(128, Art(Letzter(wieder)!).GetProperty("ownMultiplier").GetInt32());
            Assert.AreEqual(MinigamePhase.Running, server.BuzzerController!.StateManager.MinigameState.Phase,
                "Die Trennung hat die Runde geschlossen - Bert ist noch drin.");
        }
    }
}
