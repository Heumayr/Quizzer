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
    /// Die Heisse Kartoffel ueber den echten Hub (M35 C6), drei Telefone als SignalR-Clients: wandert sie, erfahren es alle
    /// Telefone ohne neuen Countdown; was ein anderer als der Halter schickt, erreicht den Spielleiter nicht; es knallt ueber
    /// die Fristwache des Servers, und danach zaehlt nichts mehr. Kein Zustand vor dem Knall nennt den Zuender.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class KartoffelHubUnitTests
    {
        private const int TestPort = 5443;
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        /// <summary>Laengste Zuendschnur 60 s, Wurf 21 337 ms ueber dem Drittel: es knallt 41,337 s nach dem Countdown.</summary>
        private static readonly TimeSpan Zuender = TimeSpan.FromMilliseconds(41_337);

        private static readonly MinispielZeile[] Aussagen =
        [
            new("Der Mond ist ein Planet.", false),
            new("Berlin liegt an der Spree.", true),
            new("Spinnen haben acht Beine.", true),
        ];

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende, TaskCompletionSource<ClientLayoutStateDto> Angemeldet);

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
            spiel.Zufall = (a, b) => b > 1000 ? 21_337 : a;
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

        private Task SendeAsync(Telefon telefon, object nutzlast)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.HotPotato,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = nutzlast,
            });

        private static JsonElement Info(ClientLayoutStateDto dto) => (JsonElement)dto.LayoutInfo!;

        private static int Phase(ClientLayoutStateDto? dto)
            => dto?.LayoutInfo is JsonElement { ValueKind: JsonValueKind.Object } info && info.TryGetProperty("phase", out var p) ? p.GetInt32() : -1;

        private static JsonElement Art(ClientLayoutStateDto dto) => Info(dto).GetProperty("kindInfo");

        private static string? Halter(ClientLayoutStateDto? dto) => dto == null ? null : Art(dto).GetProperty("holderName").GetString();

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

        [TestMethod]
        public async Task EveryPhoneSeesThePassOnlyTheHolderCountsAndTheServerLetsItBang()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);

            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(
                MinigameKind.HotPotato, "Heiße Kartoffel", "Regel", 1, DurationSeconds: 60, TriggerPlayerId: anna.Id, Share: 50, Content: Aussagen));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());

            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Running && Halter(Letzter(t)) == "Anna")),
                "Nicht jedes Telefon sieht, dass Anna haelt.");
            var aussage = Art(Letzter(a)!).GetProperty("statementText").GetString();
            var durchgang = Info(Letzter(c)!).GetProperty("durchgang").GetInt32();

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));

            // Bert schickt fuer Anna - das erreicht den Spielleiter nicht.
            await SendeAsync(b, new { statementNumber = 1, answer = true });
            await SendeAsync(a, new { statementNumber = 1, answer = Aussagen.Single(x => x.Text == aussage).Wahr });
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Art(z).GetProperty("choosing").GetBoolean()), "Anna darf nach der richtigen Antwort nicht weitergeben.");

            await SendeAsync(a, new { statementNumber = 1, passTo = bert.Id });
            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Halter(Letzter(t)) == "Bert")), "Nicht jedes Telefon sieht, dass Bert haelt.");
            Assert.IsTrue(Art(Letzter(b)!).GetProperty("isHolder").GetBoolean());
            Assert.AreEqual(durchgang, Info(Letzter(c)!).GetProperty("durchgang").GetInt32(), "Die Weitergabe startet den Countdown der Telefone neu.");

            lock (abgaben)
            {
                Assert.IsFalse(abgaben.Any(x => x.Player.Id == bert.Id), "Berts Abgabe fuer Anna erreichte den Spielleiter.");
                CollectionAssert.AreEqual(new object[] { new KartoffelAntwort(1, true), new KartoffelWeitergabe(anna.Id, bert.Id) },
                    abgaben.Select(x => x.Value).ToArray());
            }

            await Task.Delay(3 * 50);
            Assert.AreEqual(MinigamePhase.Running, ctrl.StateManager.MinigameState.Phase, "Es knallt vor dem Zuender.");

            uhr.Vor(Zuender);
            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)),
                "Der Server laesst es nicht selbst knallen.");
            lock (geschlossen)
                Assert.AreEqual(new KartoffelKnall(bert.Id, anna.Id), geschlossen.Single().Submissions[bert.Id]);

            var ende = Art(Letzter(c)!);
            Assert.IsTrue(ende.GetProperty("exploded").GetBoolean());
            Assert.AreEqual("Bert", ende.GetProperty("holderName").GetString());
            Assert.AreEqual("Anna", ende.GetProperty("passerName").GetString());
            Assert.AreEqual(41, ende.GetProperty("fuseSeconds").GetInt32());

            var vorher = Abgaben();
            await SendeAsync(b, new { statementNumber = 2, answer = true });
            await Task.Delay(200);
            Assert.AreEqual(vorher, Abgaben(), "Nach dem Knall zaehlt eine Abgabe.");

            // Vor dem Knall in keinem Zustand irgendeines Telefons: weder der Zuender noch eine Restzeit in Sekunden.
            foreach (var dto in new[] { a, b, c }.SelectMany(Alle).Where(d => Phase(d) != (int)MinigamePhase.Finished))
            {
                var zahlen = Zustandsinhalt.Von(Art(dto)).Zahlen;
                Assert.IsFalse(zahlen.Any(z => z is 41 or 42 or 41_337 or 41.337 or 40 or 40_337 or 40.337), "Vor dem Knall steht der Zuender im Zustand.");
            }
        }
    }
}
