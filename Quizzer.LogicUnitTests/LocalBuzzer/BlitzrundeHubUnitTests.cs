using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Die Blitzrunde ueber den echten Hub (M35 C5), zwei Telefone als SignalR-Clients: wer urteilt, sagt die
    /// Verbindung; nur das urteilende Telefon bekommt seine naechste Aussage; waehrend der Runde erreicht kein
    /// Ergebnis ein Telefon; und haben alle alles beurteilt, schliesst der Server die Runde selbst.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BlitzrundeHubUnitTests
    {
        private const int TestPort = 5437;
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        private static readonly MinispielZeile[] Aussagen =
        [
            new("Der Mond ist ein Planet.", false),
            new("Berlin liegt an der Spree.", true),
            new("Spinnen haben acht Beine.", true),
        ];

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

            var spiel = server.BuzzerController.StateManager.MinigameState;
            spiel.Uhr = uhr;
            spiel.Zufall = new Random(7).Next;
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
        private Task UrteileAsync(Telefon telefon, int aussage, bool wahr, Guid? alsWer = null)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.LightningRound,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                playerId = alsWer,
                payload = new { statementNumber = aussage, answer = wahr, playerId = alsWer },
            });

        private static JsonElement Info(ClientLayoutStateDto dto) => (JsonElement)dto.LayoutInfo!;

        private static int Phase(ClientLayoutStateDto? dto)
            => dto?.LayoutInfo is JsonElement { ValueKind: JsonValueKind.Object } info && info.TryGetProperty("phase", out var p) ? p.GetInt32() : -1;

        private static JsonElement Art(ClientLayoutStateDto dto) => Info(dto).GetProperty("kindInfo");

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.ToList(); }

        private int Abgaben() { lock (abgaben) return abgaben.Count; }

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

        private async Task StarteAsync()
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(
                MinigameKind.LightningRound, "Blitzrunde", "Regel", 1, DurationSeconds: 30, Content: Aussagen));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        private static bool Wahr(JsonElement art) => Aussagen.Single(a => a.Text == art.GetProperty("statementText").GetString()).Wahr;

        [TestMethod]
        public async Task OnlyTheJudgingPhoneGetsItsNextStatementAndNoResultReachesAPhoneBeforeTheEnd()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync();

            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Running)), "Die Telefone sehen die Runde nicht.");
            var bertVorher = Alle(b).Count;
            var annasErste = Art(Letzter(a)!).GetProperty("statementText").GetString();

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));
            await UrteileAsync(a, 1, true);
            await UrteileAsync(b, 1, false, alsWer: anna.Id);
            await UrteileAsync(a, 1, false);

            Assert.IsTrue(await WarteAsync(() => Abgaben() == 2), "Die Urteile erreichen den Spielleiter nicht.");
            await Task.Delay(150);
            lock (abgaben)
            {
                Assert.AreEqual(2, abgaben.Count, "Dieselbe Aussage zaehlte zweimal.");
                Assert.AreEqual(new BlitzrundeFortschritt(1, 1), abgaben.Single(x => x.Player.Id == bert.Id).Value,
                    "Der Server glaubt dem playerId in der Nachricht statt der Verbindung.");
            }

            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Art(z).GetProperty("statementNumber").GetInt32() == 2),
                "Anna bekommt ihre naechste Aussage nicht.");
            Assert.AreNotEqual(annasErste, Art(Letzter(a)!).GetProperty("statementText").GetString());
            Assert.IsTrue(await WarteAsync(() => Alle(b).Count > bertVorher), "Bert bekommt keine Antwort auf sein Urteil.");
            await Task.Delay(150);
            Assert.AreEqual(bertVorher + 1, Alle(b).Count, "Bert bekam mehr als die Antwort auf sein eigenes Urteil.");

            // Waehrend der Runde: in keinem Zustand irgendeines Telefons ein Ergebnis.
            foreach (var dto in Alle(a).Concat(Alle(b)))
            {
                var art = Art(dto);
                Assert.AreEqual(JsonValueKind.Null, art.GetProperty("ownCorrect").ValueKind, "Ein Telefon erfaehrt waehrend der Runde, was stimmte.");
                Assert.AreEqual(JsonValueKind.Null, art.GetProperty("ownWrong").ValueKind);
            }
        }

        [TestMethod]
        public async Task WhenEveryoneHasJudgedEverythingTheServerClosesAndThePhonesLearnTheirResult()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync();
            Assert.IsTrue(await WarteAsync(() => Phase(Letzter(a)) == (int)MinigamePhase.Running));

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));

            foreach (var (telefon, richtig) in new[] { (a, true), (b, false) })
            {
                for (var n = 1; n <= Aussagen.Length; n++)
                {
                    var nummer = n;
                    Assert.IsTrue(await WarteAsync(() => Letzter(telefon) is { } z && Art(z).GetProperty("statementNumber").GetInt32() == nummer),
                        $"Aussage {nummer} kommt nicht an.");
                    uhr.Vor(TimeSpan.FromMilliseconds(400));
                    await UrteileAsync(telefon, nummer, Wahr(Art(Letzter(telefon)!)) == richtig);
                }
            }

            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)),
                "Haben alle alles beurteilt, schliesst der Server die Runde nicht.");
            lock (geschlossen)
                Assert.AreEqual(1, geschlossen.Count, "Der Spielleiter erfaehrt das Ende nicht genau einmal.");

            Assert.AreEqual(3, Art(Letzter(a)!).GetProperty("ownCorrect").GetInt32(), "Anna erfaehrt ihr Ergebnis nicht.");
            Assert.AreEqual(3, Art(Letzter(b)!).GetProperty("ownWrong").GetInt32(), "Bert erfaehrt sein Ergebnis nicht.");
        }
    }
}
