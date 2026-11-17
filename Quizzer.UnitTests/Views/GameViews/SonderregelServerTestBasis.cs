using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.UnitTests.Views.BuzzerViews;
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
    public abstract class SonderregelServerTestBasis : BuzzerServerTestBasis
    {
        protected TestGameBuilder? world;
        protected BuzzerServerViewModel? serverVm;
        protected readonly List<HubConnection> phones = new();
        protected readonly List<List<ClientLayoutStateDto>> staende = new();

        /// <summary>Richtet die Zelle vor dem Oeffnen ein - Anteil, Spielweise (M35 C3b).</summary>
        protected Action<GameGridCoordinate>? ZelleEinrichten { get; set; }

        /// <summary>Schreibt vor dem Oeffnen in die Datenbank - Joker (M35 C7b).</summary>
        private protected Func<TestGameBuilder, Task>? VorDemOeffnen { get; set; }

        /// <summary>Richtet das Fragefenster vor dem Laden ein - Spielfenster, getrenntes Telefon (M37 T5).</summary>
        private protected Func<TestableCurrentQuestionViewModel, Task>? FensterEinrichten { get; set; }

        [TestInitialize]
        public void SetUpBasis()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
            Klang.Aktiv = false;
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

        /// <summary>Spiel mit <paramref name="spieler"/> Spielern, Kontostaende vorweg, Server, Telefone, Frage offen.</summary>
        private protected async Task<TestableCurrentQuestionViewModel> OeffneAsync(
            CellSpecialRule regel, int spieler = 3, int[]? kontostaende = null, Func<CellSpecialRule, SonderregelAblauf?>? fabrik = null,
            QuestionType typ = QuestionType.Default)
        {
            world = await TestGameBuilder.CreateAsync(typ, normalStepCount: 3, playerCount: spieler);
            world.Coordinate.SpecialRule = regel;
            ZelleEinrichten?.Invoke(world.Coordinate);

            if (kontostaende != null)
                SetzeKontostaende(kontostaende);

            if (VorDemOeffnen != null)
                await VorDemOeffnen(world);

            serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = world.Game;
            await StarteServerAsync(serverVm);

            foreach (var p in world.Players)
                await VerbindeAsync(p);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            if (fabrik != null)
                vm.SonderregelFabrik = fabrik;

            if (FensterEinrichten != null)
                await FensterEinrichten(vm);

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

        protected PlayerResultContext Karte(TestableCurrentQuestionViewModel vm, int spieler)
            => vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world!.Players[spieler].Id);
    }
}
