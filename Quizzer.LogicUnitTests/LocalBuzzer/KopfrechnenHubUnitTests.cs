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
    /// Das Kopfrechnen ueber den echten Hub (M35 C5), zwei Telefone als SignalR-Clients: wer antwortet, sagt die
    /// Verbindung; nur das antwortende Telefon bekommt seine naechste Aufgabe; der Server schliesst an der
    /// Frist selbst; und keine Loesung erreicht ein Telefon.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class KopfrechnenHubUnitTests
    {
        private const int TestPort = TestPorts.KopfrechnenHub;
        private const int Startwert = 123_777;
        private const int Stufe = KopfrechnenAufgaben.Mittel;
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

            var spiel = server.BuzzerController.StateManager.MinigameState;
            spiel.Uhr = uhr;
            spiel.Zufall = (_, _) => Startwert;
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
        private Task AntworteAsync(Telefon telefon, int aufgabe, int antwort, Guid? alsWer = null)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.MentalArithmetic,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                playerId = alsWer,
                payload = new { taskNumber = aufgabe, answer = antwort, playerId = alsWer },
            });

        private static int Loesung(int aufgabe) => KopfrechnenAufgaben.Aufgabe(Startwert, aufgabe, Stufe).Loesung;

        private static ClientLayoutStateDto? Letzter(Telefon t) { lock (t.Zustaende) return t.Zustaende.LastOrDefault(); }

        private static List<ClientLayoutStateDto> Alle(Telefon t) { lock (t.Zustaende) return t.Zustaende.ToList(); }

        private int Abgaben() { lock (abgaben) return abgaben.Count; }

        private async Task StarteAsync()
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(
                MinigameKind.MentalArithmetic, "Kopfrechnen", "Regel", 1, DurationSeconds: 30, Variant: Stufe));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync());
        }

        [TestMethod]
        public async Task OnlyTheAnsweringPhoneGetsItsNextTaskAndTheServerClosesAtTheDeadline()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync();

            Assert.IsTrue(await WarteAsync(() => Phase(Letzter(b)) == (int)MinigamePhase.Running), "Bert sieht die Runde nicht.");
            var bertVorher = Alle(b).Count;

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));
            await AntworteAsync(a, 1, Loesung(1));
            await AntworteAsync(b, 1, Loesung(1) + 1, alsWer: anna.Id);
            await AntworteAsync(a, 1, Loesung(1));

            Assert.IsTrue(await WarteAsync(() => Abgaben() == 2), "Die Antworten erreichen den Spielleiter nicht.");
            await Task.Delay(150);
            lock (abgaben)
            {
                Assert.AreEqual(2, abgaben.Count, "Dieselbe Aufgabe zaehlte zweimal.");
                var annas = abgaben.Single(x => x.Player.Id == anna.Id).Value;
                var berts = abgaben.Single(x => x.Player.Id == bert.Id).Value;
                Assert.AreEqual(new KopfrechnenAntwort(1, true, new(1, 1)), annas);
                Assert.AreEqual(new KopfrechnenAntwort(1, false, new(0, 1)), berts, "Der Server glaubt dem playerId in der Nachricht statt der Verbindung.");
            }

            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { } z && Art(z).GetProperty("taskNumber").GetInt32() == 2),
                "Anna bekommt ihre naechste Aufgabe nicht.");
            Assert.AreEqual(KopfrechnenAufgaben.Aufgabe(Startwert, 2, Stufe).Text, Art(Letzter(a)!).GetProperty("taskText").GetString());
            Assert.IsTrue(await WarteAsync(() => Alle(b).Count > bertVorher), "Bert bekommt keine Antwort auf seine Abgabe.");
            await Task.Delay(150);
            Assert.AreEqual(bertVorher + 1, Alle(b).Count, "Bert bekam mehr als die Antwort auf seine eigene Abgabe.");

            uhr.Vor(TimeSpan.FromSeconds(30));

            Assert.IsTrue(await WarteAsync(() => new[] { a, b }.All(t => Phase(Letzter(t)) == (int)MinigamePhase.Finished)),
                "Nach der Frist schliesst der Server die Runde nicht selbst.");
            lock (geschlossen)
                Assert.AreEqual(1, geschlossen.Count, "Der Spielleiter erfaehrt das Ende nicht genau einmal.");

            Assert.IsTrue(Art(Letzter(a)!).GetProperty("isWinner").GetBoolean(), "Anna hat die meisten richtig und nicht gewonnen.");
            Assert.IsFalse(Art(Letzter(b)!).GetProperty("isWinner").GetBoolean());

            await AntworteAsync(b, 2, Loesung(2));
            await Task.Delay(200);
            Assert.AreEqual(2, Abgaben(), "Eine Antwort nach der Frist wurde angenommen.");

            // Keine Loesung und kein Startwert in irgendeinem Zustand irgendeines Telefons.
            foreach (var dto in Alle(a).Concat(Alle(b)))
            {
                var text = JsonSerializer.Serialize(dto.LayoutInfo);
                StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex($@"\b{Startwert}\b|loesung|solution", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    "Startwert oder Loesung im Zustand eines Telefons.");
            }
        }

        [TestMethod]
        public async Task AReconnectingPhoneContinuesWithItsTask()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await StarteAsync();

            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));
            await AntworteAsync(a, 1, Loesung(1));
            Assert.IsTrue(await WarteAsync(() => Abgaben() == 1));

            await a.Verbindung.StopAsync();
            Assert.IsTrue(await WarteAsync(() => anna.ConnectionState != PlayerConnection.Connected));

            var wieder = await VerbindeAsync(anna);

            // Was ein wiederkommendes Telefon bekommt, ist der volle Zustand in "Assigned".
            var zugeordnet = await wieder.Angemeldet.Task;
            Assert.AreEqual(2, Art(zugeordnet).GetProperty("taskNumber").GetInt32(), "Das wiederverbundene Telefon faengt von vorn an.");
            Assert.AreEqual(1, Art(zugeordnet).GetProperty("ownCorrect").GetInt32());
            Assert.AreEqual(MinigamePhase.Running, server.BuzzerController!.StateManager.MinigameState.Phase,
                "Die Trennung hat die Runde geschlossen - beim Kopfrechnen laeuft sie bis zur Frist.");
        }
    }
}
