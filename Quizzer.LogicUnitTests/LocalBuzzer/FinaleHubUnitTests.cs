using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.TestShared;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Das Finale über den echten Hub (M37 E4), Telefone als SignalR-Clients: Einsätze und Antworten sieht kein anderes Telefon
    /// vor der Auflösung - auch keins, das erst danach verbindet (Gegenprobe mit zwei Inhalten); eine zweite Antwort wird
    /// verworfen, eine lange gekürzt, und wer abgegeben hat, bleibt auch nach dem Wiederverbinden gesperrt.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FinaleHubUnitTests
    {
        private const int TestPort = TestPorts.FinaleHub;

        private const int AnnasEinsatz = 137;
        private const int BertsEinsatz = 251;
        private const string AnnasAntwort = "Geheim-Anna-Ottawa";
        private const string BertsAntwort = "Geheim-Bert-Toronto";

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Staende);

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;
        private readonly List<BuzzerInputState.InputResult> eingegangen = new();
        private readonly List<HubConnection> offen = new();

        [TestInitialize]
        public async Task StartServer()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            game = new Game { Id = Guid.NewGuid(), Designation = "Finale" };

            foreach (var p in new[] { anna, bert, carl, dora })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            server = new BuzzerServer { GetGame = () => game };
            await HubTest.StartAsync(server, TestPort);
            Steuerung.EventBus.PlayerSubmittedInput += r => { lock (eingegangen) eingegangen.Add(r); };
        }

        [TestCleanup]
        public async Task StopServer()
        {
            foreach (var v in offen)
            {
                try { await v.DisposeAsync(); } catch { }
            }

            if (server != null)
                await server.DisposeAsync();
        }

        private static Player Spieler(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        private BuzzerController Steuerung => server.BuzzerController!;

        private async Task<Telefon> VerbindeAsync(Player spieler)
        {
            var telefon = new Telefon(
                new HubConnectionBuilder()
                    .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={Uri.EscapeDataString(server.AccessKeys.SchluesselFuer(spieler.Id))}")
                    .Build(),
                new List<ClientLayoutStateDto>());

            telefon.Verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (telefon.Staende) telefon.Staende.Add(dto); });
            telefon.Verbindung.On<ClientLayoutStateDto>(ClientStateSender.StateChanged, dto => { lock (telefon.Staende) telefon.Staende.Add(dto); });
            await telefon.Verbindung.StartAsync();
            offen.Add(telefon.Verbindung);

            // Der Hub traegt die Verbindung erst nach dem Handschlag ein - gewartet wird auf Assigned.
            Assert.IsTrue(await WarteAsync(() => Letzter(telefon) != null), $"{spieler.Designation} bekam keine Zuordnung.");
            return telefon;
        }

        private static ClientLayoutStateDto? Letzter(Telefon t)
        {
            lock (t.Staende) return t.Staende.Count > 0 ? t.Staende[^1] : null;
        }

        /// <summary>Alles, was dieses Telefon je bekam, entschlüsselt - Zahlen und Texte.</summary>
        private static Zustandsinhalt Alles(Telefon t)
        {
            lock (t.Staende) return Zustandsinhalt.Von(t.Staende.ToList());
        }

        private int Eingaben()
        {
            lock (eingegangen) return eingegangen.Count;
        }

        /// <summary>Wie das Fragefenster die Vorphase des Finales einstellt: Kontostände dabei, eine Runde.</summary>
        private async Task VorphaseAsync()
        {
            Steuerung.StateManager.MinigameState.Configure(new MinigameSetup(default, "Sonderregel: Finale", "Regel", 1, CountdownSeconds: 0,
                SpecialRule: CellSpecialRule.Final, FieldPoints: 100,
                Balances: new Dictionary<Guid, int> { [anna.Id] = 500, [bert.Id] = 400, [carl.Id] = 300, [dora.Id] = 200 }));
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await Steuerung.StartMinigameRoundAsync());
        }

        private Task SetzeAsync(Telefon t, int einsatz)
            => t.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = 0,
                specialRule = (int)CellSpecialRule.Final,
                durchgang = Steuerung.StateManager.MinigameState.Durchgang,
                payload = new { amount = einsatz },
            });

        /// <summary>Wie das Fragefenster die Antworten des Finales einstellt: Texteingabe, jeder genau einmal.</summary>
        private async Task AntwortenAsync()
        {
            Steuerung.StateManager.BuzzerInputState.Infos = new BuzzerInputState.BuzzerInputInfo { InputType = "text", Placeholder = "Deine Antwort fürs Finale" };
            Steuerung.StateManager.BuzzerInputState.EinmalAbgabe = true;
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);
        }

        [TestMethod]
        public async Task NeitherStakesNorAnswersReachAnotherPhoneNotEvenOneThatConnectsLater()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);

            await VorphaseAsync();
            await SetzeAsync(a, AnnasEinsatz);
            await SetzeAsync(b, BertsEinsatz);
            Assert.IsTrue(await WarteAsync(() => Steuerung.StateManager.MinigameState.Rules?.CurrentSubmissions.Count == 2), "Die Einsätze kamen nicht an.");
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { LayoutInfo: not null } && Alles(a).Zahlen.Contains(AnnasEinsatz)),
                "Anna bekommt ihren eigenen Einsatz nicht zurück - die Gegenprobe sähe nichts.");
            await Steuerung.CloseMinigameRoundAsync();

            await AntwortenAsync();
            await a.Verbindung.InvokeAsync("SubmitInput", new { value = AnnasAntwort });
            await b.Verbindung.InvokeAsync("SubmitInput", new { value = BertsAntwort });
            Assert.IsTrue(await WarteAsync(() => Eingaben() == 2), "Die Antworten erreichten den Spielleiter nicht.");

            // Erst jetzt verbindet Dora - sie bekommt den ganzen Stand auf einmal.
            var d = await VerbindeAsync(dora);
            await Task.Delay(200);

            Assert.IsFalse(Alles(a).Zahlen.Contains(BertsEinsatz) || Alles(a).EnthaeltText(BertsAntwort), "Anna sieht Berts Einsatz oder Antwort.");
            Assert.IsFalse(Alles(b).Zahlen.Contains(AnnasEinsatz) || Alles(b).EnthaeltText(AnnasAntwort), "Bert sieht Annas Einsatz oder Antwort.");

            foreach (var (telefon, name) in new[] { (c, "Carl"), (d, "Dora") })
            {
                var inhalt = Alles(telefon);
                Assert.IsFalse(inhalt.Zahlen.Contains(AnnasEinsatz) || inhalt.Zahlen.Contains(BertsEinsatz), $"{name} sieht einen Einsatz.");
                Assert.IsFalse(inhalt.EnthaeltText("Geheim-"), $"{name} sieht eine Antwort.");
            }
        }

        [TestMethod]
        public async Task ASecondAnswerIsDiscardedALongOneIsCutAndThePhoneStaysLockedAfterReconnecting()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await AntwortenAsync();

            await a.Verbindung.InvokeAsync("SubmitInput", new { value = new string('x', 30_000) });
            Assert.IsTrue(await WarteAsync(() => Eingaben() == 1), "Der lange Text kam nicht an.");
            lock (eingegangen)
                Assert.AreEqual(BuzzerInputState.MaxLength, eingegangen[0].Value.Length, "Der Spielleiter bekommt den ungekürzten Text.");

            await a.Verbindung.InvokeAsync("SubmitInput", new { value = "Ottawa" });
            await Task.Delay(200);
            Assert.AreEqual(1, Eingaben(), "Die zweite Antwort erreichte den Spielleiter.");
            Assert.AreEqual(new string('x', 80), Steuerung.StateManager.BuzzerInputState.InputsForPlayer[anna.Id].Value, "Die erste Antwort wurde ersetzt.");
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { CurrentLayoutLocked: true }), "Annas Telefon ist nach ihrer Antwort offen.");

            // Wiederverbunden: weiter gesperrt - sonst sähe es aus, als dürfe sie noch einmal.
            await a.Verbindung.DisposeAsync();
            var wieder = await VerbindeAsync(anna);
            Assert.IsTrue(Letzter(wieder) is { Layout: BuzzerControlsLayout.Input, CurrentLayoutLocked: true }, "Nach dem Wiederverbinden ist Annas Eingabe offen.");
        }
    }
}
