using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server;

namespace Quizzer.UnitTests.Server
{
    /// <summary>„Meine Statistik" mit Teams (M37 T12, F-M37-14): je Spiel das Team samt Platz, in der Kopfzeile Einzel- und Teamsiege getrennt.</summary>
    [TestClass]
    public class StatistikTeamAnzeigeUnitTests
    {
        private static GameStatistic Spiel(string name, string? team = null, int teamPlatz = 0, int teams = 0) =>
            new(Guid.NewGuid(), name, "Gina", GameState.Finished, 6, 2, 300, 3, 1, 12, 12, false, team, teamPlatz, teams);

        private static StatisticsResponse Statistik(int einzelsiege, int teamsiege, params GameStatistic[] spiele) =>
            new(Guid.NewGuid(), "Mia", spiele.Length, einzelsiege, 600, 0, spiele, new Leaderboard([], []), teamsiege);

        [TestMethod]
        public void ARowNamesTheTeamWithItsPlaceAndAGameWithoutTeamsADash()
        {
            Assert.AreEqual("Rot: 1. von 3", StatisticsViewModel.Zeile(Spiel("Teamabend", "Rot", 1, 3)).Team);
            Assert.AreEqual("-", StatisticsViewModel.Zeile(Spiel("Quizabend")).Team, "Ein Spiel ohne Teams nennt ein Team.");
            Assert.AreEqual("2. von 6", StatisticsViewModel.Zeile(Spiel("Teamabend", "Rot", 1, 3)).Platz, "Der Einzelplatz geht im Teamspiel verloren.");
        }

        [TestMethod]
        public void WithTeamGamesTheSummaryCountsSingleAndTeamWinsApart()
        {
            var mitTeams = StatisticsViewModel.Zusammenfassung(Statistik(1, 2, Spiel("Quizabend"), Spiel("Teamabend", "Rot", 1, 3)));
            StringAssert.Contains(mitTeams, "1 ohne Teams gewonnen, 2 mit dem Team gewonnen");

            var ohneTeams = StatisticsViewModel.Zusammenfassung(Statistik(1, 0, Spiel("Quizabend")));
            Assert.AreEqual("Mia: 1 Spiele gespielt, 1 gewonnen, 600 Punkte insgesamt. Als Gamemaster: 0 Spiele.", ohneTeams,
                "Ohne Teamspiel (oder von einem älteren Server) ändert sich der Satz.");

            var ohneSieg = StatisticsViewModel.Zusammenfassung(Statistik(0, 0, Spiel("Teamabend", "Blau", 2, 3)));
            StringAssert.Contains(ohneSieg, "0 mit dem Team gewonnen", "Ein Teamspiel ohne Sieg verschweigt die Teamsiege.");
        }
    }
}
