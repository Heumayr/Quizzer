using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// M33 (2026-10-23): nach Ablauf des Countdowns nehmen die Telefone nichts mehr an, bis der
    /// Spielleiter ausdrücklich freigibt - mit einem echten SignalR-Client wie ein Telefon.
    /// </summary>
    [TestClass]
    public class ZeitsperreUnitTests
    {
        private const int TestPort = 5397;

        private BuzzerServer server = null!;
        private Game game = null!;
        private Player anna = null!;
        private Player bert = null!;

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
        }

        [TestCleanup]
        public async Task StopServer()
        {
            if (server != null)
                await server.DisposeAsync();
        }

        private async Task<(HubConnection Verbindung, List<ClientLayoutStateDto> Zustaende)> VerbindeAsync(Player player)
        {
            var zustaende = new List<ClientLayoutStateDto>();
            var verbindung = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{TestPort}/hub?key={server.AccessKeys.SchluesselFuer(player.Id)}")
                .Build();

            verbindung.On<ClientLayoutStateDto>("StateChanged", dto => { lock (zustaende) zustaende.Add(dto); });
            await verbindung.StartAsync();
            return (verbindung, zustaende);
        }

        private static async Task<bool> WarteAsync(Func<bool> bedingung, int millis = 4000)
        {
            var bis = DateTime.UtcNow.AddMilliseconds(millis);
            while (DateTime.UtcNow < bis)
            {
                if (bedingung())
                    return true;
                await Task.Delay(50);
            }
            return bedingung();
        }

        private static ClientLayoutStateDto? Letzter(List<ClientLayoutStateDto> z)
        {
            lock (z) return z.LastOrDefault();
        }

        [TestMethod]
        public async Task AfterTheCountdownNoInputIsTakenUntilTheGameMasterReleases()
        {
            var (verbindung, zustaende) = await VerbindeAsync(anna);
            var ctrl = server.BuzzerController!;

            try
            {
                await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Input);
                await ctrl.SperreWegenZeitAsync();

                Assert.IsTrue(await WarteAsync(() => Letzter(zustaende) is { ZeitAbgelaufen: true, CurrentLayoutLocked: true }),
                    "Das Telefon hat die Sperre nicht bekommen.");

                await verbindung.InvokeAsync("SubmitInput", new { playerId = anna.Id, value = "42" });
                await Task.Delay(300);
                Assert.AreEqual(0, ctrl.StateManager.BuzzerInputState.InputsForPlayer.Count, "Eine Eingabe nach Ablauf wurde angenommen.");

                await ctrl.GibEingabenFreiAsync();
                Assert.IsTrue(await WarteAsync(() => Letzter(zustaende) is { ZeitAbgelaufen: false, CurrentLayoutLocked: false }),
                    "Die Freigabe kam nicht am Telefon an.");

                await verbindung.InvokeAsync("SubmitInput", new { playerId = anna.Id, value = "42" });
                Assert.IsTrue(await WarteAsync(() => ctrl.StateManager.BuzzerInputState.InputsForPlayer.ContainsKey(anna.Id)),
                    "Nach der Freigabe wurde die Eingabe nicht angenommen.");
            }
            finally
            {
                await verbindung.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task ABuzzAfterTheCountdownDoesNotWin()
        {
            var (verbindung, _) = await VerbindeAsync(bert);
            var ctrl = server.BuzzerController!;

            try
            {
                await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);
                await ctrl.SperreWegenZeitAsync();

                await verbindung.InvokeAsync("Buzz");
                await Task.Delay(300);
                Assert.IsNull(ctrl.StateManager.BuzzerState.Winner, "Ein Buzzer nach Ablauf hat gewonnen.");

                await ctrl.GibEingabenFreiAsync();
                await verbindung.InvokeAsync("Buzz");
                Assert.IsTrue(await WarteAsync(() => ctrl.StateManager.BuzzerState.Winner?.Id == bert.Id));
            }
            finally
            {
                await verbindung.DisposeAsync();
            }
        }

        [TestMethod]
        public void ANewRoundLiftsTheTimeLockAndTheLockDoesNotCloseTheRound()
        {
            var manager = new LayoutStateManager(new global::LocalBuzzer.Service.Hubs.Accessors.GameAccessor());
            manager.ResetLayouts(1, BuzzerControlsLayout.Input);

            manager.SperreWegenZeit();
            Assert.IsTrue(manager.CreateClientState().CurrentLayoutLocked);
            Assert.IsFalse(manager.AllLocked, "Die Zeitsperre darf die Runde nicht schließen - es wird nichts ausgewertet.");

            manager.ResetLayouts(2, BuzzerControlsLayout.Input);
            Assert.IsFalse(manager.ZeitAbgelaufen, "Die nächste Frage begann gesperrt.");
        }
    }
}
