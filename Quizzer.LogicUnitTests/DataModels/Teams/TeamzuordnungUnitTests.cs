using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// Wer zu welchem Team gehört (M37 T2): Teilnehmer ohne Spielleiter, der Stempel nur für ein Team dieses Spiels
    /// (A01), Startfehler und -warnungen nur im Teamspiel.
    /// </summary>
    [TestClass]
    public class TeamzuordnungUnitTests
    {
        private Game spiel = null!;
        private Player leiter = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;

        [TestInitialize]
        public void SetUp()
        {
            leiter = new Player { Id = Guid.NewGuid(), DisplayName = "Leo" };
            spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true, ModeratorPlayerId = leiter.Id };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            spiel.GameTeams.AddRange([rot, blau]);
        }

        private PlayerXGame Setze(string name, GameTeam? team)
        {
            var spieler = new Player { Id = Guid.NewGuid(), DisplayName = name };
            var platz = new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = team?.Id };
            spiel.PlayerXGames.Add(platz);
            return platz;
        }

        /// <summary>
        /// Der Spielleiter ist nie im Los: sitzt er (Altbestand) zugleich als Mitspieler am Spiel, zählt er nicht als
        /// Teilnehmer und fehlt deshalb auch nicht in einem Team.
        /// </summary>
        [TestMethod]
        public void TheModeratorIsNeverAParticipant()
        {
            Setze("Anna", rot);
            Setze("Bert", blau);
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = leiter.Id, Player = leiter });

            CollectionAssert.AreEquivalent(new[] { "Anna", "Bert" }, Teamzuordnung.Teilnehmer(spiel).Select(x => x.Player.CalculatedDisplayName).ToArray());
            Assert.AreEqual(0, Teamzuordnung.Startfehler(spiel).Count, "Der Spielleiter ohne Team hält den Start an: " + string.Join(" | ", Teamzuordnung.Startfehler(spiel)));
        }

        /// <summary>Der Stempel ist das Team des Spielers - aber nur im Teamspiel und nur ein Team dieses Spiels.</summary>
        [TestMethod]
        public void TheStampIsTheOwnTeamOfThisGameOnly()
        {
            var anna = Setze("Anna", rot);
            var bert = Setze("Bert", null);
            var fremd = Setze("Fremd", new GameTeam { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Name = "Anderswo" });

            Assert.AreEqual(rot.Id, Teamzuordnung.TeamVon(spiel, anna.PlayerId));
            Assert.IsNull(Teamzuordnung.TeamVon(spiel, bert.PlayerId), "Ohne Team kein Stempel.");
            Assert.IsNull(Teamzuordnung.TeamVon(spiel, fremd.PlayerId), "Ein Team aus einem anderen Spiel wurde als Stempel genommen.");
            Assert.IsNull(Teamzuordnung.TeamVon(spiel, Guid.NewGuid()), "Wer nicht mitspielt, bekam einen Stempel.");

            spiel.IsTeamGame = false;
            Assert.IsNull(Teamzuordnung.TeamVon(spiel, anna.PlayerId), "Ohne Teamspiel wurde trotzdem gestempelt.");
        }

        /// <summary>Ein vollständig zugeteiltes Teamspiel startet ohne Fehler und ohne Warnung.</summary>
        [TestMethod]
        public void ACompleteTeamGameHasNoFindings()
        {
            Setze("Anna", rot);
            Setze("Bert", blau);

            Assert.AreEqual(0, Teamzuordnung.Startfehler(spiel).Count);
            Assert.AreEqual(0, Teamzuordnung.Startwarnungen(spiel).Count);
        }

        /// <summary>Weniger als zwei Teams ist ein Fehler.</summary>
        [TestMethod]
        public void OneTeamIsNotEnough()
        {
            spiel.GameTeams.Remove(blau);
            Setze("Anna", rot);

            StringAssert.Contains(Teamzuordnung.Startfehler(spiel).Single(), "mindestens 2 Teams");
        }

        /// <summary>R3-H1: zwei Teams angelegt, aber alle im selben - Teamregeln und Minispiele liefen ohne Gegner.</summary>
        [TestMethod]
        public void OneOccupiedTeamIsNotEnough()
        {
            Setze("Anna", rot);
            Setze("Bert", rot);

            StringAssert.Contains(Teamzuordnung.Startfehler(spiel).Single(), "mindestens 2 Teams mit Mitgliedern, besetzt ist eins");
        }

        /// <summary>Ein Mitspieler ohne Team ist ein Fehler - beim Namen genannt. Ein Team aus einem anderen Spiel zählt nicht.</summary>
        [TestMethod]
        public void APlayerWithoutTeamIsNamed()
        {
            Setze("Anna", rot);
            Setze("Bert", blau);
            Setze("Carla", null);
            Setze("Dora", new GameTeam { Id = Guid.NewGuid(), Name = "Anderswo" });

            var fehler = Teamzuordnung.Startfehler(spiel).Single();

            StringAssert.Contains(fehler, "Carla, Dora");
            Assert.IsFalse(fehler.Contains("Anna"), fehler);
        }

        /// <summary>Ein leeres Team ist eine Warnung, kein Fehler.</summary>
        [TestMethod]
        public void AnEmptyTeamIsAWarning()
        {
            Setze("Anna", rot);
            Setze("Bert", rot);

            Assert.AreEqual(0, Teamzuordnung.Startfehler(spiel).Count);
            StringAssert.Contains(Teamzuordnung.Startwarnungen(spiel).Single(), "„Blau“");
        }

        /// <summary>Ohne Teamspiel gibt es keinen Befund - auch ohne ein einziges Team.</summary>
        [TestMethod]
        public void WithoutTeamGameThereIsNoFinding()
        {
            spiel.IsTeamGame = false;
            spiel.GameTeams.Clear();
            Setze("Anna", null);

            Assert.AreEqual(0, Teamzuordnung.Startfehler(spiel).Count);
            Assert.AreEqual(0, Teamzuordnung.Startwarnungen(spiel).Count);
        }

        /// <summary>Die erste freie Farbe der Palette - und keine, wenn alle vergeben sind.</summary>
        [TestMethod]
        public void TheFirstFreeColourIsOfferedUntilThePaletteIsUsedUp()
        {
            Assert.AreEqual(1, TeamFarben.ErsteFreie([0, 2])?.Index);
            Assert.IsNull(TeamFarben.ErsteFreie(Enumerable.Range(0, TeamGrenzen.MaxTeams)));
        }
    }
}
