using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Teamblock im Spielaufbau (M37 T2): Schalter, Beratungszeit, Wertung der Minispiele - am Spiel, gespeichert mit
    /// „Speichern“. Altbestand mit Beratungszeit 0 bekommt beim Einschalten die Vorgabe.
    /// </summary>
    [TestClass]
    public class SpielaufbauTeamsUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 3);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Ein Spiel wie aus der Migration: Beratungszeit 0.</summary>
        private async Task<EditGameViewModel> AltbestandAsync()
        {
            using (var ctrl = new GamesController())
            {
                var spiel = (await ctrl.GetAsync(world.Game.Id))!;
                spiel.TeamCouncilSeconds = 0;
                await ctrl.UpdateAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            var vm = new EditGameViewModel();
            await vm.LoadModel(world.Game.Id);
            return vm;
        }

        [TestMethod]
        public async Task SwitchingTeamGameOnSetsTheCouncilDefaultOnlyWhenZero()
        {
            var vm = await AltbestandAsync();

            vm.IsTeamGame = true;
            Assert.AreEqual(TeamGrenzen.DefaultCouncilSeconds, vm.TeamCouncilSeconds, "Altbestand mit 0 bekam beim Einschalten nicht die Vorgabe.");

            vm.TeamCouncilSeconds = 45;
            vm.IsTeamGame = false;
            vm.IsTeamGame = true;
            Assert.AreEqual(45, vm.TeamCouncilSeconds, "Eine gesetzte Beratungszeit wurde beim Einschalten überschrieben.");
        }

        [TestMethod]
        public async Task TheCouncilTimeStaysWithinItsLimits()
        {
            var vm = await AltbestandAsync();

            vm.TeamCouncilSeconds = 700;
            Assert.AreEqual(TeamGrenzen.MaxCouncilSeconds, vm.TeamCouncilSeconds);

            vm.TeamCouncilSeconds = -5;
            Assert.AreEqual(0, vm.TeamCouncilSeconds);
        }

        [TestMethod]
        public async Task TheSettingsGoWithSave()
        {
            var vm = await AltbestandAsync();

            vm.IsTeamGame = true;
            vm.TeamCouncilSeconds = 90;
            vm.TeamMinigameSum = true;
            vm.TeamMinigameAverage = false;

            await vm.VMSaveAsync();

            using var ctrl = new GamesController();
            var gelesen = (await ctrl.GetAsync(world.Game.Id))!;
            Assert.AreEqual((true, 90, TeamMinigameScoring.Sum), (gelesen.IsTeamGame, gelesen.TeamCouncilSeconds, gelesen.TeamMinigameScoring));
        }

        [TestMethod]
        public async Task TheOverviewSaysWhoIsWithoutTeam()
        {
            var vm = await AltbestandAsync();

            Assert.AreEqual(string.Empty, vm.TeamUebersicht, "Ohne Teamspiel steht eine Übersicht da.");

            vm.IsTeamGame = true;
            Assert.AreEqual("noch keine Teams · 3 ohne Team", vm.TeamUebersicht);
        }
    }
}
