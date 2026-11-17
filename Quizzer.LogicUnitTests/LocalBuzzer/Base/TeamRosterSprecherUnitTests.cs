using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Der Sprecher in der Aufstellung (M37 T5): nach dem Stand des Teams, für diese Frage überschreibbar (Joker-Sperre,
    /// getrennt), und am Telefon als Name - nie als Kennung.
    /// </summary>
    [TestClass]
    public class TeamRosterSprecherUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player leo = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private Game game = null!;

        [TestInitialize]
        public void Aufbau()
        {
            (anna, bert, carl, leo) = (Spieler("Anna"), Spieler("Bert"), Spieler("Carl"), Spieler("Leo"));
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true, ModeratorPlayerId = leo.Id, Moderator = leo };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            game.GameTeams.AddRange([rot, blau]);

            // Bert vor Anna eingetragen - die Folge sortiert selbst. Der Spielleiter sitzt mit am Tisch von Rot.
            foreach (var (p, team) in new[] { (bert, rot), (anna, rot), (carl, blau), (leo, rot) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team.Id });
        }

        private static Player Spieler(string name) => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name };

        [TestMethod]
        public void TheSpeakerFollowsTheTurnOfTheTeam()
        {
            Assert.AreEqual("Anna", TeamRoster.From(game).TeamOf(bert.Id)?.Speaker, "Beim Stand 0 spricht nicht die Erste nach Namen.");
            Assert.AreEqual("Carl", TeamRoster.From(game).TeamOf(carl.Id)?.Speaker);

            rot.SpeakerTurn = 1;
            Assert.AreEqual("Bert", TeamRoster.From(game).TeamOf(anna.Id)?.Speaker, "Der Stand des Teams bewegt den Sprecher nicht.");

            rot.SpeakerTurn = 2;
            Assert.AreEqual("Anna", TeamRoster.From(game).TeamOf(anna.Id)?.Speaker, "Der Spielleiter zählt in der Folge mit.");
        }

        /// <summary>Für diese Frage spricht ein anderer (der Sprecher ist per Joker gesperrt): das Telefon zeigt, wer wirklich antwortet.</summary>
        [TestMethod]
        public void TheSpeakerOfThisQuestionOverridesTheTurn()
        {
            var aufstellung = TeamRoster.From(game, new Dictionary<Guid, Guid> { [rot.Id] = bert.Id });

            Assert.AreEqual("Bert", aufstellung.TeamOf(anna.Id)?.Speaker);
            Assert.AreEqual("Carl", aufstellung.TeamOf(carl.Id)?.Speaker, "Das andere Team verlor seinen Sprecher.");
        }

        [TestMethod]
        public void ThePhoneGetsTheSpeakersNameOnly()
        {
            var info = TeamRoster.From(game).TeamOf(anna.Id)!.ToClientInfo();

            Assert.AreEqual("Anna", info.Speaker);
            Assert.AreEqual("Rot", info.Name);
        }
    }
}
