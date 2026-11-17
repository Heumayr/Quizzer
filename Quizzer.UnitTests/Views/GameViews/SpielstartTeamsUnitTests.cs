using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Spielstart im Teamspiel (M37 T2): unter zwei Teams oder ein Mitspieler ohne Team hält den Start an, ein leeres
    /// Team wird nur gefragt. Der Spielleiter ist nie im Los und fehlt deshalb in keinem Team.
    /// </summary>
    [TestClass]
    public class SpielstartTeamsUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void Setup()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void Cleanup() => UserPrompt.Reset();

        private static GameTeam Team(Game spiel, string name, int farbe) =>
            new() { Id = Guid.NewGuid(), GameId = spiel.Id, Name = name, ColorIndex = farbe, SortOrder = farbe };

        private static PlayerXGame Platz(Game spiel, string name, GameTeam? team)
        {
            var spieler = new Player { Id = Guid.NewGuid(), Designation = name };
            var platz = new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = team?.Id };
            spiel.PlayerXGames.Add(platz);
            return platz;
        }

        /// <summary>Ein Teamspiel mit zwei Teams, beide Mitspieler zugeteilt, Spielleiter gesetzt, eine Frage im Feld.</summary>
        private static (Game Spiel, GameTeam Rot, GameTeam Blau) Startbereit()
        {
            var leiter = new Player { Id = Guid.NewGuid(), Designation = "Leo" };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamabend", IsTeamGame = true, Moderator = leiter, ModeratorPlayerId = leiter.Id };
            var rot = Team(spiel, "Rot", 5);
            var blau = Team(spiel, "Blau", 4);

            spiel.GameTeams.AddRange([rot, blau]);
            Platz(spiel, "Anna", rot);
            Platz(spiel, "Bert", blau);
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = Guid.NewGuid() });

            return (spiel, rot, blau);
        }

        private string LetzterHinweis => prompt.Informs[^1].Message;

        [TestMethod]
        public void ACompleteTeamGameStarts()
        {
            Assert.IsTrue(GameMasterViewModel.Startbar(Startbereit().Spiel));
            Assert.AreEqual(0, prompt.Informs.Count, string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        [TestMethod]
        public void ATeamGameWithOneTeamDoesNotStart()
        {
            var (spiel, rot, blau) = Startbereit();
            spiel.GameTeams.Remove(blau);
            spiel.PlayerXGames.ForEach(x => x.GameTeamId = rot.Id);

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel));
            StringAssert.Contains(LetzterHinweis, "mindestens 2 Teams");
        }

        [TestMethod]
        public void APlayerWithoutTeamStopsTheStartAndIsNamed()
        {
            var (spiel, _, _) = Startbereit();
            Platz(spiel, "Carla", null);

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel));
            StringAssert.Contains(LetzterHinweis, "Ohne Team: Carla");
        }

        /// <summary>Sitzt der Spielleiter (Altbestand) zugleich am Spiel, braucht er kein Team - er ist nie im Los.</summary>
        [TestMethod]
        public void TheModeratorNeedsNoTeam()
        {
            var (spiel, _, _) = Startbereit();
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spiel.Moderator!.Id, Player = spiel.Moderator });

            Assert.IsTrue(GameMasterViewModel.Startbar(spiel), "Der Spielleiter ohne Team hält den Start an: " + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>Ohne Teamspiel zählen Teams nicht - ein Spiel ohne ein einziges Team startet wie bisher.</summary>
        [TestMethod]
        public void WithoutTeamGameTeamsDoNotMatter()
        {
            var (spiel, _, _) = Startbereit();
            spiel.IsTeamGame = false;
            spiel.GameTeams.Clear();

            Assert.IsTrue(GameMasterViewModel.Startbar(spiel));
        }

        /// <summary>
        /// Ein leeres Team hält den Start nicht an, wird aber gefragt - über den Weg, den der Start wirklich nimmt
        /// (mit der Frage aus der Datenbank). Sagt die Spielleitung nein, startet es nicht.
        /// </summary>
        [TestMethod]
        public async Task AnEmptyTeamIsAskedNotRefused()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 2);
            var leiter = new Player { Id = Guid.NewGuid(), Designation = "Leo" };
            var rot = Team(world.Game, "Rot", 5);
            var gelb = Team(world.Game, "Gelb", 3);

            world.Game.Moderator = leiter;
            world.Game.ModeratorPlayerId = leiter.Id;
            world.Game.IsTeamGame = true;
            world.Game.GameTeams.AddRange([rot, gelb]);
            world.Game.PlayerXGames.ForEach(x => x.GameTeamId = rot.Id);

            Assert.IsTrue(await GameMasterViewModel.StartbarAsync(world.Game));
            Assert.AreEqual(1, prompt.Confirms.Count, "Das leere Team wurde nicht gefragt.");
            StringAssert.Contains(prompt.Confirms[0].Message, "Team „Gelb“ hat keine Mitglieder.");

            UserPrompt.Current = prompt = new RecordingUserPrompt(answer: false);
            Assert.IsFalse(await GameMasterViewModel.StartbarAsync(world.Game), "Ein Nein zur Warnung hat trotzdem gestartet.");
        }
    }
}
