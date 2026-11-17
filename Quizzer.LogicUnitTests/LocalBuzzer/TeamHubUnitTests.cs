using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Microsoft.AspNetCore.SignalR;
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
    /// Teams über den echten Hub (M37 T4), Telefone als SignalR-Clients - die Missbrauchsfälle aus Abschnitt 4 des Entwurfs:
    /// ein Teamkollege buzzert nach dem Zurücksetzen, zwei Mitglieder drücken gleichzeitig, eine <c>teamId</c> in der
    /// Nutzlast, eine Joker-Sperre gegen den Teamkollegen (<c>TeamHubUnitTests.Joker.cs</c>). Dazu: jedes Telefon liest sein
    /// Team, die Sperre übersteht das Wiederverbinden, und die Spielleitung gibt frei.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public partial class TeamHubUnitTests
    {
        private const int TestPort = TestPorts.TeamHub;

        private sealed record Telefon(HubConnection Verbindung, List<ClientLayoutStateDto> Staende);

        private BuzzerServer server = null!;
        private Game game = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;
        private readonly List<Player?> gewinner = new();
        private readonly List<HubConnection> offen = new();

        [TestInitialize]
        public async Task StartServer()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            game = new Game { Id = Guid.NewGuid(), Designation = "Teamspiel", IsTeamGame = true };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl ist Blau, Dora ohne Team.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (dora, (GameTeam?)null) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            server = new BuzzerServer { GetGame = () => game };
            await HubTest.StartAsync(server, TestPort);
            Steuerung.EventBus.WinnerDeclared += (p, _) => { lock (gewinner) gewinner.Add(p); };
            await Steuerung.SetTeamsAsync(TeamRoster.From(game));
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

        private int Gewinnerzahl()
        {
            lock (gewinner) return gewinner.Count;
        }

        /// <summary>Anna buzzert, die Spielleitung setzt zurück - die Lage nach einer falschen Antwort.</summary>
        private async Task AnnaBuzzertUndZurueckAsync(Telefon a)
        {
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);
            await a.Verbindung.InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => Steuerung.StateManager.BuzzerState.Winner?.Id == anna.Id), "Anna kann nicht buzzern.");
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);
        }

        [TestMethod]
        public async Task EachPhoneReadsItsOwnTeam()
        {
            var a = await VerbindeAsync(anna);
            var c = await VerbindeAsync(carl);
            var d = await VerbindeAsync(dora);

            Assert.AreEqual(("Rot", "✚", 5), (Letzter(a)!.Team?.Name, Letzter(a)!.Team?.Symbol, Letzter(a)!.Team?.ColorIndex));
            Assert.AreEqual("Blau", Letzter(c)!.Team?.Name);
            Assert.IsNull(Letzter(d)!.Team, "Dora ohne Team bekommt eins.");

            // Umsetzen: Carl wechselt zu Rot - sein Telefon erfährt es sofort.
            game.PlayerXGames.Single(x => x.PlayerId == carl.Id).GameTeamId = rot.Id;
            await Steuerung.SetTeamsAsync(TeamRoster.From(game));
            Assert.IsTrue(await WarteAsync(() => Letzter(c)!.Team?.Name == "Rot"), "Carls Telefon zeigt nach dem Umsetzen das alte Team.");
        }

        [TestMethod]
        public async Task ATeammateIsRejectedAfterTheResetAndHisPhoneSaysWhy()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await AnnaBuzzertUndZurueckAsync(a);

            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { ExcludedFromQuestion: true, CurrentLayoutLocked: true }),
                "Berts Telefon sagt nicht, dass sein Team schon gebuzzert hat.");
            Assert.AreEqual(TeamBuzzLock.Satz, Letzter(b)!.ExcludedReason);
            Assert.IsTrue(await WarteAsync(() => Letzter(c) is { ExcludedFromQuestion: false, CurrentLayoutLocked: false }), "Carls Buzzer ist nicht offen.");

            var vorher = Gewinnerzahl();
            await b.Verbindung.InvokeAsync("Buzz");
            await Task.Delay(200);
            Assert.AreEqual(vorher, Gewinnerzahl(), "Bert hat für sein gesperrtes Team gebuzzert.");
            Assert.IsNull(Steuerung.StateManager.BuzzerState.Winner);

            await c.Verbindung.InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => Steuerung.StateManager.BuzzerState.Winner?.Id == carl.Id), "Carl aus dem anderen Team kann nicht buzzern.");
        }

        [TestMethod]
        public async Task TheTeamLockSurvivesAReconnect()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            await AnnaBuzzertUndZurueckAsync(a);

            var wieder = await VerbindeAsync(bert);
            Assert.AreEqual(TeamBuzzLock.Satz, Letzter(wieder)!.ExcludedReason, "Nach dem Wiederverbinden fehlt die Team-Sperre.");
            Assert.IsTrue(Letzter(wieder)!.CurrentLayoutLocked, "Nach dem Wiederverbinden ist Berts Buzzer offen.");

            await wieder.Verbindung.InvokeAsync("Buzz");
            await Task.Delay(200);
            Assert.IsNull(Steuerung.StateManager.BuzzerState.Winner, "Bert buzzert nach dem Wiederverbinden für sein gesperrtes Team.");
        }

        [TestMethod]
        public async Task TheGameMasterReleasesTheTeam()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await AnnaBuzzertUndZurueckAsync(a);
            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { ExcludedFromQuestion: true }));

            await Steuerung.ReleaseTeamsAsync();

            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { ExcludedFromQuestion: false, CurrentLayoutLocked: false }),
                "Nach dem Freigeben ist Berts Telefon weiter gesperrt.");
            await b.Verbindung.InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => Steuerung.StateManager.BuzzerState.Winner?.Id == bert.Id), "Nach dem Freigeben kann Bert nicht buzzern.");
        }

        [TestMethod]
        public async Task TwoTeammatesBuzzingAtOnceGiveExactlyOneWinner()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);

            for (var i = 0; i < 20; i++)
            {
                Steuerung.StateManager.TeamBuzzLock.Clear();
                await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);
                var vorher = Gewinnerzahl();

                await Task.WhenAll(a.Verbindung.InvokeAsync("Buzz"), b.Verbindung.InvokeAsync("Buzz"));
                Assert.IsTrue(await WarteAsync(() => Gewinnerzahl() > vorher), $"Durchlauf {i}: kein Gewinner.");
                await Task.Delay(50);
                Assert.AreEqual(vorher + 1, Gewinnerzahl(), $"Durchlauf {i}: zwei Gewinner.");

                // Danach ist das ganze Team zu, nicht nur der Gewinner.
                await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Buzzer);
                await Task.WhenAll(a.Verbindung.InvokeAsync("Buzz"), b.Verbindung.InvokeAsync("Buzz"));
                await Task.Delay(50);
                Assert.AreEqual(vorher + 1, Gewinnerzahl(), $"Durchlauf {i}: das Team buzzerte nach dem Zurücksetzen erneut.");
            }
        }

        /// <summary>Das Telefon kann sich kein Team herbeischreiben: Buzz nimmt nichts an, und die Sperre gilt weiter.</summary>
        [TestMethod]
        public async Task ABuzzWithATeamIdInThePayloadIsIgnored()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await AnnaBuzzertUndZurueckAsync(a);

            await Assert.ThrowsAsync<HubException>(() => b.Verbindung.InvokeAsync("Buzz", new { teamId = blau.Id, playerId = carl.Id }),
                "Buzz nimmt eine Nutzlast an.");
            await b.Verbindung.InvokeAsync("Buzz");
            await Task.Delay(200);

            Assert.IsNull(Steuerung.StateManager.BuzzerState.Winner, "Mit einer teamId in der Nutzlast buzzert Bert für ein fremdes Team.");
        }
    }
}
