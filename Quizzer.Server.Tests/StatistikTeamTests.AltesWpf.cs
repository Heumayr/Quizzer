using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    public sealed partial class StatistikTeamTests
    {
        /// <summary>Eine Spielzeile, wie ein WPF vom Stand <c>476ac73</c> (vor M37 T12) sie liest: ohne Team.</summary>
        private sealed record SpielzeileVorT12(Guid GameId, string GameName, string GameMasterName, GameState State, int PlayerCount,
            int Rank, int FinalScore, int RightCount, int WrongCount, int PlayedCells, int TotalCells, bool IsGameMaster);

        /// <summary>Die Antwort der Statistik vor M37 T12: ohne Teamsiege.</summary>
        private sealed record StatistikVorT12(Guid PlayerId, string PlayerName, int GamesPlayed, int GamesWon, int TotalScore,
            int GamesAsGameMaster, IReadOnlyList<SpielzeileVorT12> Games, Leaderboard Leaderboard);

        /// <summary>
        /// Das Abgleichformat bleibt, also spricht ein älteres WPF weiter mit dem Server (Rundlauf wie M37 E05). Es liest die
        /// neue Antwort ohne Fehler, sieht seinen Einzelplatz und - nach F-M37-14 - nur den Einzelsieg aus dem Spiel ohne Teams;
        /// die Teamsiege kennt es nicht.
        /// </summary>
        [TestMethod]
        public async Task AnOlderProgramReadsTheAnswerWithTeams()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaAltesWpf");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "MiaAltesWpf");
            var miaId = miaTokens.Account.PlayerId;

            var spiel = Gespielt("Altes WPF", teamspiel: true);
            var rot = Team(spiel, "Rot", 5, 0);
            var blau = Team(spiel, "Blau", 4, 1);
            var gast = Gast("Blauer Gast");
            await AnlegenAsync(gina, spiel, gast, rot, blau, Platz(spiel, miaId, rot), Platz(spiel, gast.Id, blau),
                Ergebnis(spiel, miaId, 300, rot), Ergebnis(spiel, gast.Id, 100, blau));

            var einzeln = Gespielt("Altes WPF einzeln", teamspiel: false);
            await AnlegenAsync(gina, einzeln, Platz(einzeln, miaId, null), Ergebnis(einzeln, miaId, 50, null));

            var roh = await mia.GetStringAsync("/api/v1/me/statistics");

            var neu = JsonSerializer.Deserialize<StatisticsResponse>(roh, QuizzerApiClient.Json)!;
            Assert.AreEqual(1, neu.TeamWins, "Die Probe braucht einen Teamsieg - sie misst sonst nichts.");

            var alt = JsonSerializer.Deserialize<StatistikVorT12>(roh, QuizzerApiClient.Json)!;
            var zeile = alt.Games.Single(g => g.GameId == spiel.Game.Id);
            Assert.AreEqual((2, 1, 350), (alt.GamesPlayed, alt.GamesWon, alt.TotalScore), "Ein WPF vom Stand davor liest die Summen falsch.");
            Assert.AreEqual(("Spiel Altes WPF", 1, 300, "1 / 1"), (zeile.GameName, zeile.Rank, zeile.FinalScore, $"{zeile.PlayedCells} / {zeile.TotalCells}"),
                "Ein WPF vom Stand davor liest die Spielzeile falsch.");
            Assert.IsNotNull(alt.Leaderboard, "Ein WPF vom Stand davor verliert die Bestenliste.");
        }

        /// <summary>
        /// Die Gegenrichtung: ein neues WPF am noch nicht aktualisierten Server (gleiches Abgleichformat) liest dessen Antwort
        /// ohne Teamfelder - kein Team, kein Teamsieg, der Rest wie gesendet.
        /// </summary>
        [TestMethod]
        public void TheNewProgramReadsTheAnswerOfAnOlderServer()
        {
            var spiel = new SpielzeileVorT12(Guid.NewGuid(), "Alter Abend", "Gina", GameState.Finished, 4, 1, 500, 3, 1, 9, 9, false);
            var alt = new StatistikVorT12(Guid.NewGuid(), "Mia", 1, 1, 500, 0, [spiel], new Leaderboard([], []));

            var roh = JsonSerializer.Serialize(alt, QuizzerApiClient.Json);
            var neu = JsonSerializer.Deserialize<StatisticsResponse>(roh, QuizzerApiClient.Json)!;

            var zeile = neu.Games.Single();
            Assert.AreEqual((1, 0), (neu.GamesWon, neu.TeamWins), "Die Antwort eines älteren Servers wird falsch gelesen.");
            Assert.AreEqual(("Alter Abend", 1, 500, null, 0, 0), (zeile.GameName, zeile.Rank, zeile.FinalScore, zeile.TeamName, zeile.TeamRank, zeile.TeamCount),
                "Eine Spielzeile ohne Team wird falsch gelesen.");
        }
    }
}
