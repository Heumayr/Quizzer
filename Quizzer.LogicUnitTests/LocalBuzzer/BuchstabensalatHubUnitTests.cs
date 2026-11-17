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
    /// Der Buchstabensalat ueber den echten Hub (M35 C5), zwei Telefone als SignalR-Clients: ein Fehlversuch geht
    /// nur an das eigene Telefon; ein geloester Begriff wechselt fuer alle, ohne dass sie etwas senden; die zweite
    /// richtige Eingabe zaehlt nicht; sind alle geloest, schliesst der Server; und kein offener Begriff erreicht ein
    /// Telefon.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BuchstabensalatHubUnitTests
    {
        private const int TestPort = TestPorts.BuchstabensalatHub;
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly string[] Begriffe = ["Käse", "Regenbogen"];

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
            spiel.Zufall = new Random(11).Next;
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

        private Task RateAsync(Telefon telefon, int begriff, string eingabe)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.WordScramble,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = new { termNumber = begriff, guess = eingabe },
            });

        private static int Begriff(ClientLayoutStateDto? dto) => dto == null ? -1 : Art(dto).GetProperty("termNumber").GetInt32();

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.ToList(); }

        private async Task StarteAsync()
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(
                MinigameKind.WordScramble, "Buchstabensalat", "Regel", 1, DurationSeconds: 90,
                Content: Begriffe.Select(b => new MinispielZeile(b, false)).ToList()));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        [TestMethod]
        public async Task ASolvedTermChangesForEveryPhoneAndTheSecondRightGuessDoesNotCount()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync();
            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Running)), "Die Telefone sehen die Runde nicht.");

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));

            // Ein Fehlversuch: nur Anna bekommt Antwort.
            var bertVorher = Alle(b).Count;
            await RateAsync(a, 1, "Kase");
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Art(z).GetProperty("lastWrong").GetBoolean()), "Anna erfaehrt ihren Fehlversuch nicht.");
            await Task.Delay(150);
            Assert.AreEqual(bertVorher, Alle(b).Count, "Bert bekam einen Zustand wegen Annas Fehlversuch.");

            // Anna loest - Bert bekommt den naechsten Begriff, ohne etwas zu senden.
            uhr.Vor(TimeSpan.FromSeconds(2));
            await RateAsync(a, 1, "kaese");
            Assert.IsTrue(await WarteAsync(() => Begriff(Letzter(b)) == 2), "Bert bekommt den naechsten Begriff nicht.");
            Assert.AreEqual("Anna", Art(Letzter(b)!).GetProperty("lastSolver").GetString());

            // Bert war zu spaet: seine richtige Eingabe fuer den ersten Begriff zaehlt nicht.
            await RateAsync(b, 1, "Käse");
            await Task.Delay(200);
            lock (abgaben)
            {
                Assert.AreEqual(2, abgaben.Count, "Die zu spaete Loesung erreichte den Spielleiter.");
                Assert.AreEqual(new BuchstabensalatVersuch(1, "Käse", new(1)), abgaben[1].Value);
            }

            // Bert loest den letzten: die Runde schliesst, beide sehen das Ende.
            await RateAsync(b, 2, "REGENBOGEN");
            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)),
                "Sind alle Begriffe geloest, schliesst der Server die Runde nicht.");
            lock (geschlossen)
                Assert.AreEqual(1, geschlossen.Count, "Der Spielleiter erfaehrt das Ende nicht genau einmal.");

            // Kein offener Begriff in einem Zustand: der zweite steht erst nach seiner Loesung da.
            foreach (var dto in Alle(a).Concat(Alle(b)).Where(d => Begriff(d) is 1 or 2))
            {
                var text = JsonSerializer.Serialize(dto.LayoutInfo);
                var offenerBegriff = Begriffe[Begriff(dto) - 1];
                StringAssert.DoesNotMatch(Entschluesselt(text), new System.Text.RegularExpressions.Regex(offenerBegriff, System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    $"Der offene Begriff {offenerBegriff} steht im Zustand eines Telefons.");
            }
        }

        /// <summary>Der Zustand entschluesselt - das rohe JSON kodiert Umlaute (<c>ä</c>).</summary>
        private static string Entschluesselt(string json) => string.Join("|", Zustandsinhalt.Von(JsonDocument.Parse(json).RootElement).Texte);
    }
}
