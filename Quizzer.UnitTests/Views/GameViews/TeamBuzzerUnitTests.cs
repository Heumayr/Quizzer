using LocalBuzzer.Service.Base;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.UnitTests.Views.BuzzerViews;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews;
using Quizzer.Views.StaticRessources;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Teams vom WPF bis zum Telefon (M37 T4), echter Server über das <see cref="BuzzerServerViewModel"/>: die Aufstellung kommt
    /// beim Serverstart, beim Laden eines Spiels und nach dem Umsetzen an; die Team-Sperre zeigt sich im Fragefenster, die
    /// Spielleitung gibt frei, und das Schließen der Frage leert sie (F-M37-09).
    /// </summary>
    [TestClass]
    public class TeamBuzzerUnitTests : BuzzerServerTestBasis
    {
        protected override int Port => TestPorts.TeamBuzzer;

        private TestGameBuilder world = null!;
        private BuzzerServerViewModel serverVm = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private readonly List<HubConnection> phones = new();
        private readonly List<List<ClientLayoutStateDto>> staende = new();

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
            Klang.Aktiv = false;

            world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 3);
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            // Spieler 1 und 2 sind Rot, Spieler 3 ist Blau - in der Datenbank, denn das Öffnen der Zelle stempelt.
            world.Game.IsTeamGame = true;
            world.Game.GameTeams.AddRange([rot, blau]);
            await SetzeAsync(0, rot);
            await SetzeAsync(1, rot);
            await SetzeAsync(2, blau);

            serverVm = StaticManager.BuzzerServerViewModel;
            serverVm.Game = world.Game;
        }

        [TestCleanup]
        public async Task TearDown()
        {
            foreach (var phone in phones)
            {
                try { await phone.DisposeAsync(); } catch { }
            }

            if (serverVm.IsBuzzerServerRunning)
                await ((AsyncRelayCommand)serverVm.StopServerCommand).ExecuteAsync(null);

            StaticManager.Reset();
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private async Task SetzeAsync(int spieler, GameTeam team)
        {
            world.Game.PlayerXGames.Single(x => x.PlayerId == world.Players[spieler].Id).GameTeamId = team.Id;

            await using var context = new Quizzer.Logic.Context.DataContext();
            context.PlayerXGames.Single(x => x.GameId == world.Game.Id && x.PlayerId == world.Players[spieler].Id).GameTeamId = team.Id;
            await context.SaveChangesAsync();
        }

        private async Task StarteMitTelefonenAsync()
        {
            await StarteServerAsync(serverVm);

            foreach (var p in world.Players)
            {
                var verbindung = new HubConnectionBuilder()
                    .WithUrl($"http://127.0.0.1:{Port}/hub?key={Uri.EscapeDataString(serverVm._server!.AccessKeys.SchluesselFuer(p.Id))}")
                    .Build();
                var liste = new List<ClientLayoutStateDto>();

                verbindung.On<ClientLayoutStateDto>("Assigned", dto => { lock (liste) liste.Add(dto); });
                verbindung.On<ClientLayoutStateDto>(ClientStateSender.StateChanged, dto => { lock (liste) liste.Add(dto); });
                await verbindung.StartAsync();
                phones.Add(verbindung);
                staende.Add(liste);

                Assert.IsTrue(await WarteAsync(() => Stand(phones.Count - 1) != null), $"{p.Designation} bekam keine Zuordnung.");
            }
        }

        private ClientLayoutStateDto? Stand(int telefon)
        {
            var liste = staende[telefon];
            lock (liste) return liste.Count > 0 ? liste[^1] : null;
        }

        private LayoutStateManager Hub => serverVm.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        [TestMethod]
        public async Task TheServerStartBringsEachPhoneItsTeam()
        {
            await StarteMitTelefonenAsync();

            CollectionAssert.AreEqual(new[] { "Rot", "Rot", "Blau" }, staende.Select((_, i) => Stand(i)?.Team?.Name).ToArray(),
                "Nach dem Serverstart kennen die Telefone ihr Team nicht.");
            Assert.AreEqual("✚", Stand(0)!.Team!.Symbol);
        }

        /// <summary>Ein anderes Spiel bei laufendem Server: seine Aufstellung gilt sofort, nicht die des vorigen.</summary>
        [TestMethod]
        public async Task LoadingAGameWhileTheServerRunsReplacesTheRoster()
        {
            // Ohne Telefone: deren Anmeldung legt eine Ansicht der Spielerliste auf einem fremden Thread an, und der
            // Spielwechsel hier liefe nicht auf diesem.
            await StarteServerAsync(serverVm);
            Assert.IsTrue(Hub.Teams.IsTeamGame);

            serverVm.Game = new Game { Id = Guid.NewGuid(), Designation = "Ohne Teams" };
            Assert.IsFalse(Hub.Teams.IsTeamGame, "Nach dem Laden eines Spiels ohne Teams gilt die alte Aufstellung.");

            serverVm.Game = world.Game;
            Assert.AreEqual(rot.Id, Hub.Teams.TeamOf(world.Players[0].Id)?.TeamId, "Beim Laden des Teamspiels fehlt die Aufstellung.");
        }

        [TestMethod]
        public async Task MovingTeamsSendsTheNewTeamToThePhones()
        {
            await StarteMitTelefonenAsync();
            var leiter = new GameMasterViewModel { Game = world.Game };

            world.Game.PlayerXGames.Single(x => x.PlayerId == world.Players[2].Id).GameTeamId = rot.Id;
            await leiter.TeamsUmgesetztAsync();

            Assert.IsTrue(await WarteAsync(() => Stand(2)?.Team?.Name == "Rot"), "Nach dem Umsetzen zeigt das Telefon das alte Team.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Öffnet die Zelle, Spieler 1 buzzert, die Spielleitung setzt zurück - die Lage nach einer falschen Antwort.</summary>
        private async Task<TestableCurrentQuestionViewModel> BuzzerUndZurueckAsync()
        {
            await StarteMitTelefonenAsync();
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            Assert.AreEqual(BuzzerControlsLayout.Buzzer, Hub.CurrentLayout, "Die Frage geht nicht als Buzzer auf die Telefone.");

            // Die Sichtbarkeit wird beim Lesen gerechnet - gemessen wird deshalb auch, dass das Fenster es erfährt.
            var gemeldet = new List<string?>();
            serverVm.BuzzerControlsViewModel!.PropertyChanged += (_, e) => { lock (gemeldet) gemeldet.Add(e.PropertyName); };

            await phones[0].InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => { lock (gemeldet) return gemeldet.Contains(nameof(BuzzerControlsViewModel.TeamSperreVisibility)); }),
                "Nach dem Buzzer erfährt das Fragefenster nicht, dass Rot gesperrt ist.");
            Assert.AreEqual(Visibility.Visible, serverVm.BuzzerControlsViewModel.TeamSperreVisibility);
            StringAssert.Contains(serverVm.BuzzerControlsViewModel!.TeamSperreText, "Rot");

            await serverVm.BuzzerControlsViewModel.ResetRoundAsync(BuzzerControlsLayout.Buzzer);
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.ExcludedReason == TeamBuzzLock.Satz), "Das Telefon des Teamkollegen sagt nicht, warum es gesperrt ist.");
            return vm;
        }

        [TestMethod]
        public async Task ClosingTheQuestionClearsTheTeamLock()
        {
            var vm = await BuzzerUndZurueckAsync();

            await vm.ClosedForTestAsync();

            Assert.AreEqual(0, Hub.TeamBuzzLock.LockedTeams.Count, "Nach dem Schließen der Frage bleibt Rot für die nächste gesperrt.");
            Assert.AreEqual(Visibility.Collapsed, serverVm.BuzzerControlsViewModel!.TeamSperreVisibility, "Der Knopf steht nach der Frage noch da.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        [TestMethod]
        public async Task TheReleaseButtonFreesTheTeam()
        {
            await BuzzerUndZurueckAsync();
            var steuerung = serverVm.BuzzerControlsViewModel!;
            var gemeldet = new List<string?>();
            steuerung.PropertyChanged += (_, e) => { lock (gemeldet) gemeldet.Add(e.PropertyName); };

            Assert.IsTrue(steuerung.TeamFreigebenCommand.CanExecute(null));
            await ((AsyncRelayCommand)steuerung.TeamFreigebenCommand).ExecuteAsync(null);

            Assert.IsTrue(await WarteAsync(() => Stand(1) is { ExcludedFromQuestion: false, CurrentLayoutLocked: false }),
                "Nach „Team wieder freigeben“ ist das Telefon des Teamkollegen weiter gesperrt.");
            Assert.AreEqual(Visibility.Collapsed, steuerung.TeamSperreVisibility);
            lock (gemeldet) CollectionAssert.Contains(gemeldet, nameof(BuzzerControlsViewModel.TeamSperreVisibility), "Der Knopf erfährt nicht, dass er weg muss.");

            await phones[1].InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => Hub.BuzzerState.Winner?.Id == world.Players[1].Id), "Nach dem Freigeben kann der Teamkollege nicht buzzern.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }
    }
}
