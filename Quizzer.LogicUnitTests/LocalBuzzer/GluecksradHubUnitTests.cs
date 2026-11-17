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
    /// Das Gluecksrad ueber den echten Hub (M35 C4), drei Telefone: ohne Ziel schliesst der Server die Runde
    /// selbst, wenn das Rad steht; mit Ziel waehlt nur der Dreher, und das Feld kennen die Zuschauer erst
    /// mit der Aufloesung.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class GluecksradHubUnitTests
    {
        private const int TestPort = 5423;

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private WheelSegment feld;
        private readonly StellbareUhr uhr = new();
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

            var spiel = server.BuzzerController.StateManager.MinigameState;
            spiel.Uhr = uhr;
            spiel.Zufall = (_, _) => GluecksradEvaluator.Felder.ToList().IndexOf(feld);
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
            await telefon.Verbindung.StartAsync();
            offen.Add(telefon.Verbindung);

            Assert.IsTrue(await WarteAsync(() => player.ConnectionState == PlayerConnection.Connected), $"{player.Designation} ist nicht verbunden.");
            return telefon;
        }

        private static int Phase(ClientLayoutStateDto? dto)
            => dto?.LayoutInfo is JsonElement { ValueKind: JsonValueKind.Object } info && info.TryGetProperty("phase", out var p) ? p.GetInt32() : -1;

        private static JsonElement Art(ClientLayoutStateDto dto) => ((JsonElement)dto.LayoutInfo!).GetProperty("kindInfo");

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Laufend(Telefon t)
        {
            lock (t.Zustaende)
                return t.Zustaende.Where(z => Phase(z) == (int)MinigamePhase.Running).ToList();
        }

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

        private async Task DreheAsync(WheelSegment gedreht)
        {
            feld = gedreht;
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(MinigameKind.WheelOfFortune, "Glücksrad", "Regel", 1, TriggerPlayerId: anna.Id));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        private static string? Ergebnis(Telefon t) => Letzter(t) is { } z && Phase(z) == (int)MinigamePhase.Finished
            ? Art(z).GetProperty("resultText").GetString()
            : null;

        [TestMethod]
        public async Task WithoutTargetTheServerClosesWhenTheWheelStops()
        {
            var telefone = new[] { await VerbindeAsync(anna), await VerbindeAsync(bert), await VerbindeAsync(carl) };
            await DreheAsync(WheelSegment.Double);

            Assert.IsTrue(await WarteAsync(() => telefone.All(t => Laufend(t).Count > 0)), "Nicht jedes Telefon sieht die laufende Runde.");
            await Task.Delay(3 * 50);
            Assert.AreEqual(MinigamePhase.Running, server.BuzzerController!.StateManager.MinigameState.Phase, "Die Runde schliesst, bevor das Rad steht.");

            uhr.Vor(TimeSpan.FromSeconds(3));

            Assert.IsTrue(await WarteAsync(() => telefone.All(t => Ergebnis(t) == "Verdoppeln – Anna bekommt den doppelten Anteil.")),
                "Steht das Rad, schliesst der Server die Runde nicht selbst.");
            Assert.IsTrue(telefone.SelectMany(Laufend).All(z => Art(z).GetProperty("segmentName").ValueKind == JsonValueKind.Null),
                "Ein Telefon kennt das Feld, bevor der Beamer es zeigt.");
        }

        [TestMethod]
        public async Task OnlyTheSpinnerChoosesAndTheWatchersLearnTheFieldAtTheEnd()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await DreheAsync(WheelSegment.Steal);
            uhr.Vor(TimeSpan.FromSeconds(3));

            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { CurrentLayoutLocked: false } z && Art(z).GetProperty("choosesTarget").GetBoolean()),
                "Der Dreher bekommt keine Zielwahl.");
            Assert.AreEqual(2, Art(Letzter(a)!).GetProperty("options").GetArrayLength());
            Assert.IsTrue(Letzter(b)!.CurrentLayoutLocked, "Ein Zuschauer ist offen.");

            // Bert gibt sich als Anna aus - die Verbindung sagt, wer abgibt.
            await b.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.WheelOfFortune,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                playerId = anna.Id,
                payload = new { chosen = carl.Id, playerId = anna.Id },
            });
            await Task.Delay(200);
            lock (abgaben)
                Assert.AreEqual(0, abgaben.Count, "Ein Zuschauer hat fuer den Dreher gewaehlt.");

            await a.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.WheelOfFortune,
                durchgang = server.BuzzerController.StateManager.MinigameState.Durchgang,
                payload = new { chosen = bert.Id },
            });

            Assert.IsTrue(await WarteAsync(() => new[] { a, b, c }.All(t => Ergebnis(t) == "Klauen – Anna beklaut Bert.")),
                "Die Zielwahl schliesst die Runde nicht oder die Aufloesung kommt nicht an.");
            Assert.AreEqual("Klauen", Art(Letzter(c)!).GetProperty("segmentName").GetString());
            Assert.IsTrue(new[] { b, c }.SelectMany(Laufend).All(z => Art(z).GetProperty("segmentName").ValueKind == JsonValueKind.Null),
                "Ein Zuschauer kennt das Feld vor der Aufloesung.");
            Assert.IsTrue(Laufend(c).Count > 0, "Carl hat die laufende Runde nie gesehen - die Probe misst nichts.");
        }
    }
}
