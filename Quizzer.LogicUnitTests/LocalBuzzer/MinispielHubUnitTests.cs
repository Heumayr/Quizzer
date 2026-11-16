using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Der Minispiel-Rahmen ueber den echten Hub (M35 B3), mit SignalR-Clients wie Telefonen:
    /// <c>SpielAktion</c>, der Zustand je Verbindung, die Wiederverbindung mit eigener Abgabe und das
    /// getrennte Telefon.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public partial class MinispielHubUnitTests
    {
        private const int TestPort = 5411;

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende, List<ClientLayoutStateDto> Anmeldungen);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
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
            await server.StartAsync(new BuzzerServerOptions { Port = TestPort, BindAddress = "127.0.0.1" });

            server.BuzzerController!.EventBus.MinigameSubmitted += a => { lock (abgaben) abgaben.Add(a); };
            server.BuzzerController.EventBus.MinigameRoundClosed += r => { lock (geschlossen) geschlossen.Add(r); };
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

            Assert.IsTrue(await WarteAsync(() => Anzahl(telefon.Anmeldungen) > 0), "Das Telefon wurde nicht angemeldet.");
            return telefon;
        }

        private async Task StarteReaktionAsync(int runden = 2)
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.Reaction, "Reaktion", "Regel", runden));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync(), "Die Runde liess sich nicht starten.");
        }

        private Task AbgebenAsync(Telefon telefon, int ms, object? extra = null)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.Reaction,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = new { falseStart = false, milliseconds = ms },
                playerId = extra,
            });

        private static int Anzahl<T>(List<T> liste) { lock (liste) return liste.Count; }

        private static ClientLayoutStateDto? Letzter(List<ClientLayoutStateDto> z) { lock (z) return z.LastOrDefault(); }

        private static JsonElement Info(ClientLayoutStateDto? dto) => (JsonElement)dto!.LayoutInfo!;

        private static bool Abgegeben(ClientLayoutStateDto? dto)
            => dto?.LayoutInfo is JsonElement info && info.GetProperty("ownSubmitted").GetBoolean();

        private static async Task<bool> WarteAsync(Func<bool> bedingung, int millis = 4000)
        {
            var bis = DateTime.UtcNow.AddMilliseconds(millis);
            while (DateTime.UtcNow < bis)
            {
                if (bedingung())
                    return true;
                await Task.Delay(30);
            }
            return bedingung();
        }

        [TestMethod]
        public async Task EachPhoneGetsItsOwnState()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteReaktionAsync();

            Assert.IsTrue(await WarteAsync(() => Letzter(b.Zustaende) is { Layout: BuzzerControlsLayout.Minigame, CurrentLayoutLocked: false }),
                "Bert bekommt die laufende Runde nicht offen.");
            var vorher = Anzahl(b.Zustaende);

            await AbgebenAsync(a, 300);

            Assert.IsTrue(await WarteAsync(() => Abgegeben(Letzter(a.Zustaende))), "Anna bekommt ihre Abgabe nicht zurueck.");
            var annas = Letzter(a.Zustaende)!;
            Assert.IsTrue(annas.CurrentLayoutLocked, "Nach der Abgabe ist Annas Telefon noch offen.");
            Assert.AreEqual(300, Info(annas).GetProperty("kindInfo").GetProperty("ownMilliseconds").GetInt32());
            Assert.AreEqual("Anna", annas.PlayerName, "Der Zustand je Verbindung nennt nicht den eigenen Namen.");

            await Task.Delay(300);
            Assert.AreEqual(vorher, Anzahl(b.Zustaende), "Bert bekam mitten in seiner Wartezeit einen neuen Zustand.");
            Assert.IsFalse(Abgegeben(Letzter(b.Zustaende)), "Bert sieht Annas Abgabe als seine.");
        }

        [TestMethod]
        public async Task ASubmissionSpeaksOnlyForItsOwnConnection()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await StarteReaktionAsync();

            await AbgebenAsync(a, 300, extra: bert.Id);

            Assert.IsTrue(await WarteAsync(() => Anzahl(abgaben) == 1), "Die Abgabe kam nicht beim Spielleiter an.");
            Assert.AreEqual(anna.Id, abgaben[0].Player.Id, "Die Nachricht hat bestimmt, fuer wen abgegeben wird.");
            Assert.IsFalse(server.BuzzerController!.StateManager.MinigameState.Rules!.HasSubmitted(bert.Id));

            // Doppelabgabe: verworfen, und der Spielleiter erfaehrt nichts davon.
            await AbgebenAsync(a, 120);
            await Task.Delay(300);
            Assert.AreEqual(1, Anzahl(abgaben), "Eine zweite Abgabe derselben Runde erreichte den Spielleiter.");
        }

        [TestMethod]
        public async Task AReconnectingPhoneGetsItsOwnSubmissionBack()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await StarteReaktionAsync();

            await AbgebenAsync(a, 345);
            Assert.IsTrue(await WarteAsync(() => Anzahl(abgaben) == 1));

            await a.Verbindung.StopAsync();
            var wieder = await VerbindeAsync(anna);

            var zurueck = Letzter(wieder.Anmeldungen)!;
            Assert.IsTrue(Abgegeben(zurueck), "Das wiederkommende Telefon weiss nichts von seiner Abgabe.");
            Assert.AreEqual(345, Info(zurueck).GetProperty("kindInfo").GetProperty("ownMilliseconds").GetInt32());
            Assert.IsTrue(zurueck.CurrentLayoutLocked, "Nach dem Wiederkommen liesse sich ein zweites Mal abgeben.");
        }

        [TestMethod]
        public async Task ADisconnectedPhoneSubmitsNothingAndTheRoundStillCloses()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteReaktionAsync();

            await AbgebenAsync(a, 300);
            Assert.IsTrue(await WarteAsync(() => Anzahl(abgaben) == 1));

            await b.Verbindung.StopAsync();

            Assert.IsTrue(await WarteAsync(() => Anzahl(geschlossen) == 1), "Die Runde wartet auf ein getrenntes Telefon.");
            Assert.IsFalse(geschlossen[0].Submissions.ContainsKey(bert.Id), "Fuer das getrennte Telefon steht eine Abgabe da.");
            Assert.IsTrue(await WarteAsync(() =>
                Letzter(a.Zustaende)?.LayoutInfo is JsonElement i && i.GetProperty("phase").GetInt32() == (int)MinigamePhase.RoundOver),
                "Annas Telefon erfaehrt nicht, dass die Runde zu ist.");
        }

        [TestMethod]
        public async Task ALockedPlayerIsLockedOnlyOnHisOwnPhone()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);

            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.Reaction, "Reaktion", "Regel", 1));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            ctrl.StateManager.MinigameState.LockPlayer(bert.Id);
            await ctrl.StartMinigameRoundAsync();

            Assert.IsTrue(await WarteAsync(() => Letzter(b.Zustaende) is { CurrentLayoutLocked: true }
                                              && Letzter(a.Zustaende) is { CurrentLayoutLocked: false }),
                "Die Sperre je Spieler kommt nicht genau bei Bert an.");

            await AbgebenAsync(b, 200);
            await Task.Delay(300);
            Assert.AreEqual(0, Anzahl(abgaben), "Der gesperrte Spieler hat abgegeben.");
        }

        [TestMethod]
        public async Task AProbePhoneLinkSignsInAsItsPlayer()
        {
            var links = ProbePhoneLinks.For(game.Players, server.AccessKeys, $"127.0.0.1:{TestPort}");
            var link = links.Single(l => l.Player.Id == bert.Id).Url;

            var schluessel = System.Web.HttpUtility.ParseQueryString(link.Query)["key"];
            var assigned = new List<ClientLayoutStateDto>();
            var verbindung = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={Uri.EscapeDataString(schluessel!)}")
                .Build();
            verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (assigned) assigned.Add(dto); });
            offen.Add(verbindung);

            await verbindung.StartAsync();

            Assert.IsTrue(await WarteAsync(() => Anzahl(assigned) == 1), "Mit dem Link des Probetelefons kommt man nicht hinein.");
            Assert.AreEqual("Bert", assigned[0].PlayerName);
        }
    }
}
