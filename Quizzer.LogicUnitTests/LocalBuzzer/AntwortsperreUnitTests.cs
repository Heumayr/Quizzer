using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
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
    /// Sperre je Spieler fuer die gewoehnliche Frage (M35 C3b): bei Auktion, Experte und Allianz duerfen nur
    /// einige antworten. Am Server geprueft (eine Abgabe eines Gesperrten wird verworfen und erreicht den
    /// Spielleiter nicht), am Telefon sichtbar und nach dem Wiederverbinden erhalten - echter Hub, echte Telefone.
    /// </summary>
    [TestClass]
    public class AntwortsperreUnitTests
    {
        private const int TestPort = TestPorts.Antwortsperre;

        private BuzzerServer server = null!;
        private Player anna = null!;
        private Player bert = null!;
        private readonly List<HubConnection> telefone = new();

        [TestInitialize]
        public async Task StartServer()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };

            var game = new Game { Id = Guid.NewGuid(), Designation = "Testspiel" };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            server = new BuzzerServer { GetGame = () => game };
            await HubTest.StartAsync(server, TestPort);
        }

        [TestCleanup]
        public async Task StopServer()
        {
            foreach (var t in telefone)
            {
                try { await t.DisposeAsync(); } catch { }
            }

            if (server != null)
                await server.DisposeAsync();
        }

        private BuzzerController Steuerung => server.BuzzerController!;

        /// <summary>Ein Telefon; es schreibt jeden Zustand mit, den es bekommt - Zuordnung und Aenderungen.</summary>
        private async Task<(HubConnection Verbindung, List<ClientLayoutStateDto> Staende)> VerbindeAsync(Player spieler)
        {
            var verbindung = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={server.AccessKeys.SchluesselFuer(spieler.Id)}")
                .Build();
            var staende = new List<ClientLayoutStateDto>();

            verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (staende) staende.Add(dto); });
            verbindung.On<ClientLayoutStateDto>(ClientStateSender.StateChanged, dto => { lock (staende) staende.Add(dto); });

            await verbindung.StartAsync();
            telefone.Add(verbindung);

            Assert.IsTrue(await WarteAsync(() => { lock (staende) return staende.Count > 0; }), $"{spieler.Designation} bekam keine Zuordnung.");
            return (verbindung, staende);
        }

        private static ClientLayoutStateDto Letzter(List<ClientLayoutStateDto> staende)
        {
            lock (staende) return staende[^1];
        }

        [TestMethod]
        public async Task AnExcludedPlayerCannotBuzzAndSeesWhy()
        {
            var (annaTelefon, annaStaende) = await VerbindeAsync(anna);
            var (bertTelefon, bertStaende) = await VerbindeAsync(bert);

            await Steuerung.SetAnswerersAsync([anna.Id]);
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);

            Assert.IsTrue(await WarteAsync(() => Letzter(bertStaende).Layout == BuzzerControlsLayout.Buzzer));
            Assert.IsTrue(Letzter(bertStaende).ExcludedFromQuestion, "Berts Telefon sagt nicht, dass andere antworten.");
            Assert.IsTrue(Letzter(bertStaende).CurrentLayoutLocked, "Berts Telefon ist nicht gesperrt.");

            // Annas Runde kommt über ihre eigene Verbindung - nicht unbedingt vor Berts. Ihr Stand davor ist gesperrt (V08).
            Assert.IsTrue(await WarteAsync(() => Letzter(annaStaende).Layout == BuzzerControlsLayout.Buzzer), "Annas Telefon bekam die Runde nicht.");
            Assert.IsFalse(Letzter(annaStaende).ExcludedFromQuestion, "Anna ist ausgeschlossen, obwohl sie antwortet.");
            Assert.IsFalse(Letzter(annaStaende).CurrentLayoutLocked, "Annas Buzzer ist gesperrt.");

            // Bert drueckt trotzdem (das Telefon kommt aus dem WLAN): verworfen, die Runde bleibt Anna.
            await bertTelefon.InvokeAsync("Buzz");
            await Task.Delay(200);
            Assert.IsNull(Steuerung.StateManager.BuzzerState.Winner, "Ein gesperrter Spieler hat gebuzzert.");

            await annaTelefon.InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => Steuerung.StateManager.BuzzerState.Winner?.Id == anna.Id), "Anna kann nicht buzzern.");
        }

        [TestMethod]
        public async Task AnExcludedSelectionIsDroppedAndTheRoundClosesWithoutHim()
        {
            var (annaTelefon, _) = await VerbindeAsync(anna);
            var (bertTelefon, _) = await VerbindeAsync(bert);
            var gemeldet = new List<Guid>();
            var alleAbgegeben = 0;
            Steuerung.EventBus.PlayerSelectedKeys += r => { lock (gemeldet) gemeldet.Add(r.PlayerId); };
            Steuerung.EventBus.AllPlayersSelectedKeys += (_, _) => Interlocked.Increment(ref alleAbgegeben);

            Steuerung.StateManager.BuzzerKeySelector.Infos.KeysAndDesignations = new() { ["A"] = "Eins", ["B"] = "Zwei" };
            await Steuerung.SetAnswerersAsync([anna.Id]);
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);

            await bertTelefon.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "A" } });
            await Task.Delay(200);
            lock (gemeldet) Assert.AreEqual(0, gemeldet.Count, "Die Wahl eines Gesperrten erreichte den Spielleiter.");
            Assert.IsFalse(Steuerung.StateManager.BuzzerKeySelector.KeyResultsForPlayer.ContainsKey(bert.Id), "Die Wahl eines Gesperrten zaehlt.");

            // Anna ist die einzige, die antworten darf - mit ihrer Wahl ist die Runde zu.
            await annaTelefon.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "B" } });
            Assert.IsTrue(await WarteAsync(() => Volatile.Read(ref alleAbgegeben) == 1), "Die Runde wartet auf den Gesperrten.");
        }

        [TestMethod]
        public async Task AnExcludedInputIsDroppedAndTheLockSurvivesResetAndReconnect()
        {
            var (annaTelefon, _) = await VerbindeAsync(anna);
            var (bertTelefon, _) = await VerbindeAsync(bert);
            var gemeldet = new List<Guid>();
            var alleAbgegeben = 0;
            Steuerung.EventBus.PlayerSubmittedInput += r => { lock (gemeldet) gemeldet.Add(r.PlayerId); };
            Steuerung.EventBus.AllPlayersSubmittedInput += (_, _) => Interlocked.Increment(ref alleAbgegeben);

            await Steuerung.SetAnswerersAsync([anna.Id]);
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);

            // „Runde zuruecksetzen" mitten in der Frage hebt die Sperre nicht auf.
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);

            await bertTelefon.InvokeAsync("SubmitInput", new { value = "42" });
            await Task.Delay(200);
            lock (gemeldet) Assert.AreEqual(0, gemeldet.Count, "Die Eingabe eines Gesperrten erreichte den Spielleiter.");

            // Bert verbindet neu: er kommt gesperrt an, mit dem Satz.
            var (_, neu) = await VerbindeAsync(bert);
            Assert.IsTrue(neu[0].ExcludedFromQuestion, "Nach dem Wiederverbinden fehlt die Sperre.");
            Assert.IsTrue(neu[0].CurrentLayoutLocked, "Nach dem Wiederverbinden ist Berts Eingabe offen.");

            await annaTelefon.InvokeAsync("SubmitInput", new { value = "7" });
            Assert.IsTrue(await WarteAsync(() => Volatile.Read(ref alleAbgegeben) == 1), "Die Runde wartet auf den Gesperrten.");

            // Die Frage ist zu: niemand mehr gesperrt.
            await Steuerung.SetAnswerersAsync(null);
            await Steuerung.ResetRoundAsync(2, BuzzerControlsLayout.Input);
            Assert.IsFalse(Steuerung.StateManager.CreateClientState(bert).ExcludedFromQuestion, "Die Sperre bleibt nach der Frage stehen.");
        }
    }
}

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    using global::LocalBuzzer.Service.Base.States;
    using global::LocalBuzzer.Service.Hubs.Accessors;

    /// <summary>Dieselbe Sperre ohne Hub - der Zustand selbst verwirft, auch wenn der Hub-Riegel fehlte.</summary>
    [TestClass]
    public class AntwortsperreZustandUnitTests
    {
        private static (AnswerLock Sperre, GameAccessor Zugang, Player Anna, Player Bert) Aufbau()
        {
            var anna = new Player { Id = Guid.NewGuid(), Designation = "Anna" };
            var bert = new Player { Id = Guid.NewGuid(), Designation = "Bert" };
            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            var sperre = new AnswerLock();
            sperre.Restrict([anna.Id]);
            return (sperre, new GameAccessor { GetGame = () => game }, anna, bert);
        }

        [TestMethod]
        public void TheStatesThemselvesDropAnExcludedPlayer()
        {
            var (sperre, zugang, anna, bert) = Aufbau();

            var buzzer = new BuzzerState(zugang, sperre);
            Assert.IsFalse(buzzer.TryBuzz(bert), "Der Buzzer nimmt einen Gesperrten.");
            Assert.IsTrue(buzzer.TryBuzz(anna));

            var tasten = new BuzzerKeySelector(zugang, sperre);
            tasten.Infos.KeysAndDesignations = new() { ["A"] = "Eins" };
            tasten.SetSelectedKeys(new() { Player = bert, PlayerId = bert.Id, SelectedKeys = ["A"] });
            Assert.IsFalse(tasten.KeyResultsForPlayer.ContainsKey(bert.Id), "Die Tastenwahl nimmt einen Gesperrten.");
            tasten.SetSelectedKeys(new() { Player = anna, PlayerId = anna.Id, SelectedKeys = ["A"] });
            Assert.IsTrue(tasten.Locked, "Die Tastenwahl wartet auf den Gesperrten.");

            var eingabe = new BuzzerInputState(zugang, sperre);
            eingabe.SetInput(new() { Player = bert, PlayerId = bert.Id, Value = "1" });
            Assert.IsFalse(eingabe.InputsForPlayer.ContainsKey(bert.Id), "Die Eingabe nimmt einen Gesperrten.");
            eingabe.SetInput(new() { Player = anna, PlayerId = anna.Id, Value = "2" });
            Assert.IsTrue(eingabe.Locked, "Die Eingabe wartet auf den Gesperrten.");
        }
    }
}
