using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Joker;
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
    /// Die Joker-Runde ueber den echten Hub (M35 C7b), drei Telefone als SignalR-Clients: wer abgibt, sagt die
    /// Verbindung; die Wahl erreicht kein fremdes Telefon, auch kein wiederkommendes; und der Gesperrte liest am
    /// Telefon, durch wen er gesperrt ist.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class JokerHubUnitTests
    {
        private const int TestPort = 5447;

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende, List<ClientLayoutStateDto> Anmeldungen);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private readonly JokerOffer annasSperre = new(Guid.NewGuid(), JokerKind.Block);
        private readonly JokerOffer bertsDoppelt = new(Guid.NewGuid(), JokerKind.Double);
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
            await server.StartAsync(new BuzzerServerOptions { Port = TestPort, BindAddress = "127.0.0.1" });
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

        private async Task StarteRundeAsync()
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(default, "Joker-Runde", "Regel", 1, CountdownSeconds: 0,
                Jokers: new Dictionary<Guid, IReadOnlyList<JokerOffer>> { [anna.Id] = [annasSperre], [bert.Id] = [bertsDoppelt] }));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        private Task SendeAsync(Telefon telefon, object nutzlast, Guid? behauptet = null)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = 0,
                prePhase = (int)MinigamePrePhase.Joker,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = nutzlast,
                playerId = behauptet,
            });

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static int Anzahl(Telefon t) { lock (t.Zustaende) return t.Zustaende.Count; }

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
        public async Task TheChoiceCountsForTheConnectionAndReachesNoOtherPhone()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await StarteRundeAsync();

            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { CurrentLayoutLocked: false }), "Bert bekommt die Joker-Runde nicht.");
            Assert.IsTrue(await WarteAsync(() => Letzter(c) is { CurrentLayoutLocked: true }), "Carl ohne Joker ist offen.");
            var bertVorher = Anzahl(b);
            var carlVorher = Anzahl(c);

            // Anna behauptet, Bert zu sein, und schickt seinen Joker - verworfen, denn es zaehlt die Verbindung.
            await SendeAsync(a, new { jokerId = bertsDoppelt.Id }, behauptet: bert.Id);
            await SendeAsync(a, new { jokerId = annasSperre.Id, target = carl.Id }, behauptet: bert.Id);

            Assert.IsTrue(await WarteAsync(() => { lock (abgaben) return abgaben.Count == 1; }), "Annas eigene Sperre erreicht den Spielleiter nicht.");
            lock (abgaben)
            {
                Assert.AreEqual(anna.Id, abgaben[0].Player.Id, "Die Abgabe zaehlt nicht fuer die Verbindung.");
                Assert.AreEqual(new JokerChoice(anna.Id, annasSperre.Id, JokerKind.Block, carl.Id), abgaben[0].Value);
            }

            await Task.Delay(200);
            Assert.AreEqual(bertVorher, Anzahl(b), "Bert bekam nach Annas Wahl einen neuen Zustand.");
            Assert.AreEqual(carlVorher, Anzahl(c), "Carl bekam nach Annas Wahl einen neuen Zustand.");

            // Carl kommt mitten in der Runde wieder - der volle Zustand, aber nichts von Annas Wahl.
            var wieder = await VerbindeAsync(carl);
            List<ClientLayoutStateDto> anmeldung;
            lock (wieder.Anmeldungen) anmeldung = wieder.Anmeldungen.ToList();
            var inhalt = Zustandsinhalt.Von(anmeldung[^1].LayoutInfo);

            Assert.IsFalse(inhalt.EnthaeltText(annasSperre.Id.ToString()) || inhalt.EnthaeltText("Sperre") || inhalt.EnthaeltText("Anna"),
                "Ein wiederkommendes Telefon sieht Annas Wahl: " + string.Join(", ", inhalt.Texte));
        }

        [TestMethod]
        public async Task TheBlockedPhoneReadsWhoBlockedIt()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);

            var satz = "Anna hat dich per Joker gesperrt – du schaust zu.";
            await server.BuzzerController!.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);
            await server.BuzzerController.SetAnswerersAsync([anna.Id, carl.Id], new Dictionary<Guid, string> { [bert.Id] = satz });

            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { ExcludedFromQuestion: true }), "Bert ist nicht gesperrt.");
            Assert.AreEqual(satz, Letzter(b)!.ExcludedReason, "Bert liest nicht, wer ihn gesperrt hat.");
            Assert.IsTrue(Letzter(b)!.CurrentLayoutLocked);
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { ExcludedFromQuestion: false }));
            Assert.IsNull(Letzter(a)!.ExcludedReason, "Anna bekommt Berts Satz.");

            // Der Spielleiter hebt die Sperre auf - der Satz geht mit.
            await server.BuzzerController.SetAnswerersAsync(null);
            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { ExcludedFromQuestion: false, ExcludedReason: null }), "Nach dem Aufheben bleibt Berts Satz.");
        }
    }
}
