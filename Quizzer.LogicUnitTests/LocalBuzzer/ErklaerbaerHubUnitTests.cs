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
    /// Der Erklaerbaer ueber den echten Hub (M35 C6), drei Telefone als SignalR-Clients: nur das Telefon der Erklaererin
    /// bekommt den Begriff; ihr eigener Versuch erreicht den Spielleiter nicht; der erste Treffer schliesst die Runde, und
    /// erst dann steht der Begriff bei allen.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ErklaerbaerHubUnitTests
    {
        private const int TestPort = TestPorts.ErklaerbaerHub;
        private const string Begriff = "Leuchtturm";
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

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
            server.BuzzerController.StateManager.MinigameState.Uhr = uhr;
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

        private Task RateAsync(Telefon telefon, string eingabe)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.ExplainIt,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = new { guess = eingabe },
            });

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.ToList(); }

        private static string Text(ClientLayoutStateDto dto)
            => string.Join("|", Zustandsinhalt.Von(JsonDocument.Parse(JsonSerializer.Serialize(dto.LayoutInfo)).RootElement).Texte);

        [TestMethod]
        public async Task OnlyTheExplainersPhoneGetsTheTermAndHerGuessDoesNotCount()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);

            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(
                MinigameKind.ExplainIt, "Erklärbär", "Regel", 1, DurationSeconds: 60, TriggerPlayerId: anna.Id,
                Content: [new MinispielZeile(Begriff, false)]));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());

            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Running)), "Die Telefone sehen die Runde nicht.");
            Assert.AreEqual(Begriff, Art(Letzter(a)!).GetProperty("term").GetString(), "Die Erklaererin bekommt den Begriff nicht.");

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));
            await RateAsync(a, Begriff);
            await RateAsync(c, "Turm");
            Assert.IsTrue(await WarteAsync(() => Letzter(c) is { } z && Art(z).GetProperty("lastWrong").GetBoolean()), "Carl erfaehrt seinen Fehlversuch nicht.");
            lock (abgaben)
                Assert.IsFalse(abgaben.Any(x => x.Player.Id == anna.Id), "Der Versuch der Erklaererin erreichte den Spielleiter.");

            uhr.Vor(TimeSpan.FromSeconds(2));
            await RateAsync(b, "leuchtturm");
            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)), "Der Treffer schliesst die Runde nicht.");
            lock (geschlossen)
                Assert.AreEqual(new ErklaerbaerVersuch(true, anna.Id), geschlossen.Single().Submissions[bert.Id]);

            // Vor dem Ende kein Begriff bei Bert und Carl; danach bei allen - sonst misst die Suche nichts.
            foreach (var dto in Alle(b).Concat(Alle(c)).Where(d => Phase(d) != (int)MinigamePhase.Finished))
                Assert.IsFalse(Text(dto).Contains(Begriff, StringComparison.OrdinalIgnoreCase), "Der Begriff steht vor dem Ende im Zustand eines Ratenden.");
            Assert.IsTrue(Text(Letzter(c)!).Contains(Begriff), "Nach dem Ende steht die Aufloesung nicht da.");
        }
    }
}
