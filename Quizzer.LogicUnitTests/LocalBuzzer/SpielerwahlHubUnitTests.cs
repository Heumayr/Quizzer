using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.TestShared;
using System.Text.Json;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Das Duell ueber den echten Hub (M35 C2), drei Telefone als SignalR-Clients: die Gegnerwahl erreicht
    /// jedes Telefon, der Zuschauer bleibt gesperrt, und kein Telefon bekommt einen fremden Zug, bevor die
    /// Runde zu ist.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class SpielerwahlHubUnitTests
    {
        private const int TestPort = TestPorts.SpielerwahlHub;

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende, List<ClientLayoutStateDto> Anmeldungen);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private readonly List<MinigameSubmission> abgaben = new();
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
                new List<ClientLayoutStateDto>(), new List<ClientLayoutStateDto>());

            telefon.Verbindung.On<ClientLayoutStateDto>("StateChanged", dto => { lock (telefon.Zustaende) telefon.Zustaende.Add(dto); });
            telefon.Verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (telefon.Anmeldungen) telefon.Anmeldungen.Add(dto); });
            await telefon.Verbindung.StartAsync();
            offen.Add(telefon.Verbindung);

            Assert.IsTrue(await WarteAsync(() => { lock (telefon.Anmeldungen) return telefon.Anmeldungen.Count > 0; }), $"{player.Designation} ist nicht angemeldet.");
            return telefon;
        }

        private Task SendeAsync(Telefon telefon, object nutzlast)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.Duel,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = nutzlast,
            });

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<string> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.Select(z => ((JsonElement)z.LayoutInfo!).GetRawText()).ToList(); }

        private static JsonElement Art(ClientLayoutStateDto? dto) => ((JsonElement)dto!.LayoutInfo!).GetProperty("kindInfo");

        private static int Durchgang(ClientLayoutStateDto? dto) => ((JsonElement)dto!.LayoutInfo!).GetProperty("durchgang").GetInt32();

        [TestMethod]
        public async Task TheDuelRunsOverTheHubAndKeepsTheMovesSecret()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);

            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.Duel, "Duell", "Regel", 3, TriggerPlayerId: anna.Id));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());

            // --- Die Wahl: nur Anna bekommt Tasten, Bert und Carl schauen zu ---
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { CurrentLayoutLocked: false } z && Art(z).GetProperty("choosesOpponent").GetBoolean()),
                "Anna bekommt die Gegnerwahl nicht.");
            Assert.AreEqual(2, Art(Letzter(a)).GetProperty("options").GetArrayLength());
            Assert.IsTrue(await WarteAsync(() => Letzter(c) is { CurrentLayoutLocked: true }), "Carl ist vor der Wahl offen.");

            await SendeAsync(a, new { chosen = bert.Id });

            var nachWahl = ctrl.StateManager.MinigameState.Durchgang;
            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Letzter(t) is { } z && Durchgang(z) == nachWahl)),
                "Nicht jedes Telefon bekommt den neuen Durchgang nach der Wahl.");
            Assert.IsFalse(Letzter(b)!.CurrentLayoutLocked, "Bert ist nach der Wahl gesperrt.");
            Assert.IsTrue(Letzter(c)!.CurrentLayoutLocked, "Der Zuschauer ist nach der Wahl offen.");
            StringAssert.Contains(((JsonElement)Letzter(c)!.LayoutInfo!).GetProperty("watcherText").GetString(), "Anna und Bert");
            Assert.IsTrue(await WarteAsync(() => { lock (abgaben) return abgaben.Any(x => x.Value is PlayerChosen); }),
                "Die Wahl erreicht den Spielleiter nicht.");

            // --- Anna zieht: niemand ausser ihr erfaehrt es ---
            await SendeAsync(c, new { move = 1 });
            await SendeAsync(a, new { move = 2 });
            await SendeAsync(a, new { move = 3 });

            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Art(z).GetProperty("ownMove").GetInt32() == 2));
            await Task.Delay(200);
            Assert.IsFalse(Alle(b).Concat(Alle(c)).Any(j => j.Contains("Move\":2")), "Bert oder Carl bekam Annas Zug vor der Aufloesung.");
            lock (abgaben)
                Assert.AreEqual(1, abgaben.Count(x => x.Value is Quizzer.DataModels.Questions.Minispiele.SchereSteinPapier),
                    "Zuschauerzug oder zweiter Zug wurde angenommen.");

            // Ein Telefon, das mitten in der Runde wiederkommt, bekommt den vollen Zustand - Annas Zug darf nicht darin stehen.
            b = await VerbindeAsync(bert);
            string wiederAngemeldet;
            lock (b.Anmeldungen) wiederAngemeldet = ((JsonElement)b.Anmeldungen[0].LayoutInfo!).GetRawText();
            Assert.IsFalse(wiederAngemeldet.Contains("Move\":2"), "Berts wiederverbundenes Telefon bekommt Annas Zug vor der Aufloesung.");
            Assert.IsFalse(b.Anmeldungen[0].CurrentLayoutLocked, "Bert ist nach dem Wiederverbinden gesperrt.");

            // --- Bert zieht: die Runde ist zu, jetzt sehen beide den fremden Zug ---
            await SendeAsync(b, new { move = 1 });

            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { } z && Art(z).GetProperty("otherMove").ValueKind == JsonValueKind.Number),
                "Nach der Runde fehlt Bert der Zug des Gegners.");
            Assert.AreEqual(2, Art(Letzter(b)).GetProperty("otherMove").GetInt32());
            Assert.AreEqual(MinigamePhase.RoundOver, ctrl.StateManager.MinigameState.Phase);
        }
    }
}
