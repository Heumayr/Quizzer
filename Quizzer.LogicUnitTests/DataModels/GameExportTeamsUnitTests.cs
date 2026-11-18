using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Teams im Bündel (M37 T1): ein Import prüft dieselben Grenzen wie der Server, bevor die Datenbank es mit einem
    /// rohen Fehler täte; ein Bündel von vor T1 bleibt lesbar und ergibt ein Spiel ohne Teams.
    /// </summary>
    [TestClass]
    public class GameExportTeamsUnitTests
    {
        private static GameExportDocument Dokument()
        {
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamabend", IsTeamGame = true };
            spiel.GameTeams.Add(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5 });
            return GameExportMapper.ToDocument(spiel, [], [], null, _ => null);
        }

        [TestMethod]
        public void AValidTeamGamePasses()
        {
            GameExportSerializer.Pruefe(Dokument());
        }

        [TestMethod]
        [DataRow("", 0, DisplayName = "leerer Name")]
        [DataRow("123456789012345678901234567890X", 0, DisplayName = "31 Zeichen")]
        [DataRow("Rot", 8, DisplayName = "Farbe 8")]
        [DataRow("Rot", -1, DisplayName = "Farbe -1")]
        public void AnInvalidTeamIsRejected(string name, int farbe)
        {
            var dokument = Dokument();
            dokument.Teams.Single().Name = name;
            dokument.Teams.Single().ColorIndex = farbe;

            Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
        }

        /// <summary>R1-7: die Aufstellung als Ganzes - Rot (Farbe 5) steht schon im Bündel.</summary>
        [TestMethod]
        [DataRow("Grün", 5, 0, DisplayName = "Farbe doppelt")]
        [DataRow(" rot ", 2, 0, DisplayName = "Name doppelt")]
        [DataRow("Neun", 0, 7, DisplayName = "neuntes Team")]
        public void ALineupBreakingTheLimitsIsRejected(string name, int farbe, int weitere)
        {
            var dokument = Dokument();
            dokument.Teams.AddRange(Enumerable.Range(0, weitere).Select(i => new GameExportDocument.TeamData { Name = "Team " + i, ColorIndex = i < 5 ? i : i + 1 }));
            GameExportSerializer.Pruefe(dokument);
            dokument.Teams.Add(new GameExportDocument.TeamData { Name = name, ColorIndex = farbe });

            Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
        }

        [TestMethod]
        [DataRow(2, 30, DisplayName = "unbekannte Wertung")]
        [DataRow(0, -1, DisplayName = "Beratung negativ")]
        [DataRow(0, 601, DisplayName = "Beratung über 600 s")]
        public void InvalidTeamSettingsAreRejected(int wertung, int sekunden)
        {
            var dokument = Dokument();
            dokument.Spiel.TeamMinigameScoring = (TeamMinigameScoring)wertung;
            dokument.Spiel.TeamCouncilSeconds = sekunden;

            Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
        }

        /// <summary>Die Grenzen selbst gehen durch: 0 (ohne Countdown) und 600 s.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(600)]
        public void TheCouncilLimitsThemselvesPass(int sekunden)
        {
            var dokument = Dokument();
            dokument.Spiel.TeamCouncilSeconds = sekunden;

            GameExportSerializer.Pruefe(dokument);
        }

        /// <summary>Ein Bündel von vor T1 kennt weder Teams noch Teameinstellungen - es bleibt lesbar, als Spiel ohne Teams.</summary>
        [TestMethod]
        public void ABundleWithoutTeamsStaysReadable()
        {
            var knoten = System.Text.Json.Nodes.JsonNode.Parse(GameExportSerializer.ToJson(Dokument()))!.AsObject();
            Assert.IsTrue(knoten.Remove(nameof(GameExportDocument.Teams)), "Die Teams standen gar nicht im Bündel.");
            var spiel = knoten[nameof(GameExportDocument.Spiel)]!.AsObject();

            foreach (var feld in new[] { "IsTeamGame", "TeamMinigameScoring", "TeamCouncilSeconds" })
                Assert.IsTrue(spiel.Remove(feld), $"{feld} stand gar nicht im Bündel - dann misst diese Probe kein altes Bündel.");

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(knoten.ToJsonString()));
            var bausatz = GameExportMapper.ToEntities(GameExportSerializer.Read(strom), _ => null);

            Assert.AreEqual((false, TeamMinigameScoring.Average, 30, 0),
                (bausatz.Spiel.IsTeamGame, bausatz.Spiel.TeamMinigameScoring, bausatz.Spiel.TeamCouncilSeconds, bausatz.Spiel.GameTeams.Count));
        }
    }
}
