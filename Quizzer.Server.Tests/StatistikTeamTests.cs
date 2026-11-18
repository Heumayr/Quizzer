using Quizzer.Client;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Teamsiege in der Statistik (M37 T12, Entwurf Abschnitt 5, F-M37-14): je Spiel das Team des Kontos und sein Platz, dazu
    /// die Teamsiege; Einzelsiege zählen nur in Spielen ohne Teams. Gerechnet wie am Beamer (Stempel, F-M37-07) und nur über
    /// die Spiele, die die Statistik zeigt (A01). Eigene Klasse hinter <c>ClientSafetyTests</c>: hier sitzen Gäste, und jedes
    /// Konto sieht alle Mitspieler.
    /// </summary>
    [TestClass]
    public sealed partial class StatistikTeamTests
    {
        private static async Task PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

            var result = (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
        }

        private static async Task<StatisticsResponse> StatistikAsync(HttpClient client)
            => (await client.GetFromJsonAsync<StatisticsResponse>("/api/v1/me/statistics", QuizzerApiClient.Json))!;

        /// <summary>Ein Spiel, dessen eine Zelle gespielt ist - ein Spiel ohne gespielte Zelle ist kein Sieg (wie bei den Einzelsiegen).</summary>
        private static SyncTestData.GameSet Gespielt(string name, bool teamspiel)
        {
            var spiel = SyncTestData.NewGame(name);
            spiel.Game.IsTeamGame = teamspiel;
            spiel.Cell.IsDone = true;
            return spiel;
        }

        private static GameTeam Team(SyncTestData.GameSet spiel, string name, int farbe, int reihe)
            => new() { Id = Guid.NewGuid(), GameId = spiel.Game.Id, Name = name, ColorIndex = farbe, SortOrder = reihe };

        private static PlayerXGame Platz(SyncTestData.GameSet spiel, Guid spieler, GameTeam? team)
            => new() { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = spieler, GameTeamId = team?.Id };

        /// <summary>Ein Ergebnis an der einen Zelle, mit oder ohne Stempel (das Team beim Öffnen der Zelle).</summary>
        private static QuestionResult Ergebnis(SyncTestData.GameSet spiel, Guid spieler, int punkte, GameTeam? stempel)
            => new()
            {
                Id = Guid.NewGuid(), GameId = spiel.Game.Id, GameGridCoordinateId = spiel.Cell.Id, QuestionBaseId = spiel.Question.Id,
                PlayerId = spieler, Score = punkte, RightCount = 1, GameTeamId = stempel?.Id,
            };

        private static Player Gast(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        private static async Task<SyncTestData.GameSet> AnlegenAsync(HttpClient gamemaster, SyncTestData.GameSet spiel, params object[] zeilen)
        {
            var push = spiel.AsNewPush();
            push.Upserts.AddRange(zeilen.Select(SyncTestData.New));
            await PushAsync(gamemaster, push);
            return spiel;
        }

        /// <summary>
        /// F-M37-14: Mia gewinnt mit Team Rot (150 + Gast 150 gegen Bobs 250), Bob ist einzeln vorn - beides ist im Teamspiel
        /// kein Einzelsieg. Jedes Konto erfährt sein Team und dessen Platz; Bobs Sieg in einem Spiel ohne Teams zählt weiter
        /// als Einzelsieg und hat kein Team.
        /// </summary>
        [TestMethod]
        public async Task ATeamWinCountsForTheMembersAndAWinInATeamGameIsNoSingleWin()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaTeamsieg");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "MiaTeamsieg");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobTeamsieg");
            var miaId = miaTokens.Account.PlayerId;
            var bobId = bobTokens.Account.PlayerId;

            var teamspiel = Gespielt("Teamsieg", teamspiel: true);
            var rot = Team(teamspiel, "Rot", 5, 0);
            var blau = Team(teamspiel, "Blau", 4, 1);
            var max = Gast("Max");
            await AnlegenAsync(gina, teamspiel, max, rot, blau,
                Platz(teamspiel, miaId, rot), Platz(teamspiel, max.Id, rot), Platz(teamspiel, bobId, blau),
                Ergebnis(teamspiel, miaId, 150, rot), Ergebnis(teamspiel, max.Id, 150, rot), Ergebnis(teamspiel, bobId, 250, blau));

            var einzeln = Gespielt("Einzelsieg", teamspiel: false);
            await AnlegenAsync(gina, einzeln, Platz(einzeln, bobId, null), Platz(einzeln, miaId, null),
                Ergebnis(einzeln, bobId, 300, null), Ergebnis(einzeln, miaId, 100, null));

            var miasStatistik = await StatistikAsync(mia);
            var miaImTeamspiel = miasStatistik.Games.Single(g => g.GameId == teamspiel.Game.Id);
            Assert.AreEqual(("Rot", 1, 2), (miaImTeamspiel.TeamName, miaImTeamspiel.TeamRank, miaImTeamspiel.TeamCount),
                "Mias Team steht nicht als Sieger da.");
            Assert.AreEqual(2, miaImTeamspiel.Rank, "Der Einzelplatz im Teamspiel ist verloren gegangen - die Punkte bleiben am Spieler.");
            Assert.AreEqual((1, 0), (miasStatistik.TeamWins, miasStatistik.GamesWon), "Mias Teamsieg zählt nicht oder als Einzelsieg.");

            var bobsStatistik = await StatistikAsync(bob);
            var bobImTeamspiel = bobsStatistik.Games.Single(g => g.GameId == teamspiel.Game.Id);
            Assert.AreEqual(("Blau", 2), (bobImTeamspiel.TeamName, bobImTeamspiel.TeamRank), "Bobs Team steht falsch.");
            Assert.AreEqual(1, bobImTeamspiel.Rank, "Bob ist einzeln vorn - die Probe misst sonst nichts.");
            Assert.AreEqual((0, 1), (bobsStatistik.TeamWins, bobsStatistik.GamesWon),
                "Bobs Einzelplatz im Teamspiel zählt als Sieg, oder sein Sieg ohne Teams fehlt.");

            var bobEinzeln = bobsStatistik.Games.Single(g => g.GameId == einzeln.Game.Id);
            Assert.AreEqual((null, 0, 0), (bobEinzeln.TeamName, bobEinzeln.TeamRank, bobEinzeln.TeamCount), "Ein Spiel ohne Teams nennt ein Team.");
        }

        /// <summary>
        /// F-M37-07: Gast Carl holt 200 für Rot und wechselt danach zu Blau - die Punkte bleiben bei Rot (Stempel), wie am Beamer.
        /// Über die Zuteilung gerechnet gewänne Blau (250 + 200 gegen 100). Ein Ergebnis ohne Stempel zählt beim aktuellen Team.
        /// </summary>
        [TestMethod]
        public async Task PointsStayWithTheTeamOfTheStampLikeOnTheBeamer()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaStempel");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "MiaStempel");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobStempel");
            var miaId = miaTokens.Account.PlayerId;
            var bobId = bobTokens.Account.PlayerId;

            var spiel = Gespielt("Stempel", teamspiel: true);
            var rot = Team(spiel, "Rot", 5, 0);
            var blau = Team(spiel, "Blau", 4, 1);
            var carl = Gast("Carl");
            await AnlegenAsync(gina, spiel, carl, rot, blau,
                Platz(spiel, miaId, rot), Platz(spiel, carl.Id, blau), Platz(spiel, bobId, blau),
                Ergebnis(spiel, miaId, 100, rot), Ergebnis(spiel, carl.Id, 200, rot), Ergebnis(spiel, bobId, 250, stempel: null));

            Assert.AreEqual(1, (await StatistikAsync(mia)).Games.Single(g => g.GameId == spiel.Game.Id).TeamRank,
                "Carls Punkte sind mit ihm zu Blau gewandert.");
            Assert.AreEqual(2, (await StatistikAsync(bob)).Games.Single(g => g.GameId == spiel.Game.Id).TeamRank,
                "Bobs Ergebnis ohne Stempel zählt nicht bei seinem Team, oder Carls Punkte zählen bei Blau.");
        }

        /// <summary>Gleichstand teilt den Platz (wie die Rangfolge am Beamer): beide Teams sind Erste, beide haben einen Teamsieg.</summary>
        [TestMethod]
        public async Task ATieMakesBothTeamsWinners()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaGleichstand");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "MiaGleichstand");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobGleichstand");

            var spiel = Gespielt("Gleichstand", teamspiel: true);
            var rot = Team(spiel, "Rot", 5, 0);
            var blau = Team(spiel, "Blau", 4, 1);
            var gelb = Team(spiel, "Gelb", 3, 2);
            var gast = Gast("Gelber Gast");
            await AnlegenAsync(gina, spiel, gast, rot, blau, gelb,
                Platz(spiel, miaTokens.Account.PlayerId, rot), Platz(spiel, bobTokens.Account.PlayerId, blau), Platz(spiel, gast.Id, gelb),
                Ergebnis(spiel, miaTokens.Account.PlayerId, 200, rot), Ergebnis(spiel, bobTokens.Account.PlayerId, 200, blau),
                Ergebnis(spiel, gast.Id, 100, gelb));

            foreach (var (konto, name) in new[] { (mia, "Mia"), (bob, "Bob") })
            {
                var statistik = await StatistikAsync(konto);
                var zeile = statistik.Games.Single(g => g.GameId == spiel.Game.Id);
                Assert.AreEqual((1, 3, 1), (zeile.TeamRank, zeile.TeamCount, statistik.TeamWins), $"{name}s Team ist bei Gleichstand nicht Erster.");
            }
        }

        /// <summary>R3-1: Rot −200, Blau −100, Gelb leer - wie am Beamer zählt das leere Team nicht, Blau ist Sieger von zwei Teams.</summary>
        [TestMethod]
        public async Task AnEmptyTeamIsNotRankedLikeOnTheBeamer()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaLeeresTeam");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobLeeresTeam");
            var bobId = bobTokens.Account.PlayerId;

            var spiel = Gespielt("Leeres Team", teamspiel: true);
            var rot = Team(spiel, "Rot", 5, 0);
            var blau = Team(spiel, "Blau", 4, 1);
            var gelb = Team(spiel, "Gelb", 3, 2);
            var anna = Gast("Anna");
            var annasMinus = Ergebnis(spiel, anna.Id, 0, rot);
            var bobsMinus = Ergebnis(spiel, bobId, 0, blau);
            (annasMinus.MinusScore, bobsMinus.MinusScore) = (200, 100);
            await AnlegenAsync(gina, spiel, anna, rot, blau, gelb, Platz(spiel, anna.Id, rot), Platz(spiel, bobId, blau), annasMinus, bobsMinus);

            var statistik = await StatistikAsync(bob);
            var zeile = statistik.Games.Single(g => g.GameId == spiel.Game.Id);
            Assert.AreEqual(("Blau", 1, 2, 1), (zeile.TeamName, zeile.TeamRank, zeile.TeamCount, statistik.TeamWins),
                "Das leere Team Gelb steht in der Statistik vor Blau oder zählt mit.");
        }

        /// <summary>Ohne gespielte Zelle ist ein Teamspiel kein Sieg, auch wenn alle Teams mit 0 gleichauf vorn liegen.</summary>
        [TestMethod]
        public async Task AnUnplayedTeamGameIsNoTeamWin()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaUngespielt");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "MiaUngespielt");

            var spiel = SyncTestData.NewGame("Ungespielt");
            spiel.Game.IsTeamGame = true;
            var rot = Team(spiel, "Rot", 5, 0);
            var blau = Team(spiel, "Blau", 4, 1);
            await AnlegenAsync(gina, spiel, rot, blau, Platz(spiel, miaTokens.Account.PlayerId, rot));

            var statistik = await StatistikAsync(mia);
            Assert.AreEqual(1, statistik.Games.Single(g => g.GameId == spiel.Game.Id).TeamRank, "Ohne Punkte liegen alle Teams gleichauf vorn.");
            Assert.AreEqual(0, statistik.TeamWins, "Ein ungespieltes Teamspiel zählt als Sieg.");
        }
    }
}
