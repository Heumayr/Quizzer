using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Teams umsetzen“ im Spielfenster (M37 T2): nur im Teamspiel zu sehen, und der Knopf sagt, wer ohne Team ist -
    /// etwa wer sich mitten im Spiel am Telefon dazugemeldet hat. Den Dialog selbst prüft <c>TeamsDialogUnitTests</c>.
    /// </summary>
    [TestClass]
    public class TeamsUmsetzenUnitTests
    {
        [TestInitialize]
        public void Setup() => UserPrompt.Current = new RecordingUserPrompt(answer: true);

        [TestCleanup]
        public void Cleanup() => UserPrompt.Reset();

        private static Game Teamspiel(bool alleZugeteilt)
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5 };
            spiel.GameTeams.Add(rot);

            foreach (var name in new[] { "Anna", "Bert" })
            {
                var spieler = new Player { Id = Guid.NewGuid(), Designation = name };
                var zugeteilt = alleZugeteilt || name == "Anna";
                spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = zugeteilt ? rot.Id : null });
            }

            return spiel;
        }

        [TestMethod]
        public void TheButtonShowsOnlyInATeamGame()
        {
            var vm = new GameMasterViewModel { Game = Teamspiel(alleZugeteilt: true) };
            Assert.AreEqual(Visibility.Visible, vm.TeamsVisibility);

            vm.Game!.IsTeamGame = false;
            Assert.AreEqual(Visibility.Collapsed, vm.TeamsVisibility, "Ohne Teamspiel steht der Knopf im Spielfenster.");
        }

        [TestMethod]
        public void TheButtonSaysWhoIsWithoutTeam()
        {
            Assert.AreEqual("Teams umsetzen …", new GameMasterViewModel { Game = Teamspiel(alleZugeteilt: true) }.TeamsUmsetzenText);
            Assert.AreEqual("Teams umsetzen … (1 ohne Team)", new GameMasterViewModel { Game = Teamspiel(alleZugeteilt: false) }.TeamsUmsetzenText);
        }
    }
}
