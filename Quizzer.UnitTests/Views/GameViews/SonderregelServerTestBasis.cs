using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.Sonderregeln;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System.Net;
using System.Net.Sockets;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Gemeinsamer Aufbau der Sonderregel-Tests mit echtem Buzzer-Server (M35 C3b): Spiel, Server auf eigenem
    /// Port, je Spieler ein Telefon als SignalR-Client, das jeden Zustand mitschreibt. Je Testklasse ein Port.
    /// </summary>
    public abstract class SonderregelServerTestBasis
    {
        protected TestGameBuilder? world;
        protected BuzzerServerViewModel? serverVm;
        protected readonly List<HubConnection> phones = new();
        protected readonly List<List<ClientLayoutStateDto>> staende = new();

        /// <summary>Der Port gehoert allein der abgeleiteten Klasse.</summary>
        protected abstract int Port { get; }

        [TestInitialize]
        public void SetUpBasis()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
            Klang.Aktiv = false;

            Assert.IsTrue(PortIsFree(Port), $"Port {Port} ist belegt - er gehoert allein dieser Testklasse.");
        }

        [TestCleanup]
        public async Task TearDownBasis()
        {
            foreach (var phone in phones)
            {
                try { await phone.DisposeAsync(); } catch { }
            }
            phones.Clear();
            staende.Clear();

            if (serverVm != null)
                await ((AsyncRelayCommand)serverVm.StopServerCommand).ExecuteAsync(null);

            StaticManager.Reset();
            serverVm = null;

            if (world != null)
                await world.DisposeAsync();

            UserPrompt.Reset();
        }

        private static bool PortIsFree(int port)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>Spiel mit <paramref name="spieler"/> Spielern, Kontostaende vorweg, Server, Telefone, Frage offen.</summary>
        private protected async Task<TestableCurrentQuestionViewModel> OeffneAsync(
            CellSpecialRule regel, int spieler = 3, int[]? kontostaende = null, Func<CellSpecialRule, SonderregelAblauf?>? fabrik = null,
            QuestionType typ = QuestionType.Default)
        {
            world = await TestGameBuilder.CreateAsync(typ, normalStepCount: 3, playerCount: spieler);
            world.Coordinate.SpecialRule = regel;

            if (kontostaende != null)
                SetzeKontostaende(kontostaende);

            serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = world.Game;
            serverVm.BuzzerPort = Port;
            await ((AsyncRelayCommand)serverVm.StartServerCommand).ExecuteAsync(null);

            foreach (var p in world.Players)
                await VerbindeAsync(p);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            if (fabrik != null)
                vm.SonderregelFabrik = fabrik;

            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            return vm;
        }

        /// <summary>Kontostaende aus frueheren Zellen - nur im Speicher, gezaehlt wie gebuchte Punkte.</summary>
        private void SetzeKontostaende(int[] werte)
        {
            var frueher = new GameGridCoordinate { Id = Guid.NewGuid(), Game = world!.Game, GameId = world.Game.Id, IsDone = true };

            for (var i = 0; i < werte.Length; i++)
                frueher.QuestionResults.Add(new QuestionResult { Id = Guid.NewGuid(), PlayerId = world.Players[i].Id, Correction = werte[i] });

            world.Game.GameGridCoordinates.Add(frueher);
        }

        protected async Task VerbindeAsync(Player player)
        {
            var verbindung = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{Port}/hub?key={Uri.EscapeDataString(serverVm!._server!.AccessKeys.SchluesselFuer(player.Id))}")
                .Build();
            var liste = new List<ClientLayoutStateDto>();

            verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (liste) liste.Add(dto); });
            verbindung.On<ClientLayoutStateDto>("StateChanged", dto => { lock (liste) liste.Add(dto); });

            await verbindung.StartAsync();
            phones.Add(verbindung);
            staende.Add(liste);

            Assert.IsTrue(await WarteAsync(() => player.ConnectionState == PlayerConnection.Connected),
                $"{player.Designation} ist nicht verbunden.");
        }

        /// <summary>Der letzte Zustand, den dieses Telefon bekam.</summary>
        protected ClientLayoutStateDto? Stand(int telefon)
        {
            var liste = staende[telefon];
            lock (liste) return liste.Count > 0 ? liste[^1] : null;
        }

        protected MinigameState Zustand => serverVm!.BuzzerControlsViewModel!.MinigameState!;

        protected Task AbgebenAsync(int telefon, CellSpecialRule regel, object payload)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = 0,
                specialRule = (int)regel,
                durchgang = Zustand.Durchgang,
                payload,
            });

        protected static async Task<bool> WarteAsync(Func<bool> bedingung, int millis = 4000)
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

        protected PlayerResultContext Karte(TestableCurrentQuestionViewModel vm, int spieler)
            => vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world!.Players[spieler].Id);
    }
}
