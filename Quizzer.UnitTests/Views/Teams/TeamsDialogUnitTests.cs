using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.Teams;

namespace Quizzer.UnitTests.Views.Teams
{
    /// <summary>
    /// Der Dialog „Teams bearbeiten / umsetzen“ (M37 T2): Entwurf bis „Übernehmen“, Auslosen legt fehlende Teams an,
    /// der Spielleiter steht nicht zur Wahl, Fehler kommen als Satz (A10) statt als Absturz.
    /// </summary>
    [TestClass]
    public class TeamsDialogUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 5);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private TeamsViewModel Dialog(int saat = 7)
        {
            var vm = new TeamsViewModel { Zufall = new Random(saat) };
            vm.Lade(world.Game, "Teams bearbeiten");
            return vm;
        }

        private async Task<List<GameTeam>> TeamsInDerDatenbankAsync()
        {
            using var ctrl = new GameTeamsController();
            return await ctrl.GetForGameAsync(world.Game.Id);
        }

        [TestMethod]
        public void TheModeratorIsNotOffered()
        {
            world.Game.ModeratorPlayerId = world.Players[4].Id;

            var vm = Dialog();

            Assert.AreEqual(4, vm.Mitglieder.Count);
            Assert.IsFalse(vm.Mitglieder.Any(m => m.PlayerId == world.Players[4].Id), "Der Spielleiter steht zur Wahl.");
        }

        /// <summary>
        /// Auslosen legt die fehlenden Teams an und teilt alle zu (3/2) - geschrieben wird erst mit „Übernehmen“, und dann
        /// trägt das geladene Spiel den neuen Stand: die nächste Zelle stempelt das neue Team.
        /// </summary>
        [TestMethod]
        public async Task DrawingFillsTheDraftAndApplyingWritesItAndUpdatesTheGame()
        {
            world.Game.IsTeamGame = true;
            var vm = Dialog();
            vm.AuslosAnzahl = 2;

            vm.DrawCommand.Execute(null);

            Assert.AreEqual(2, vm.Teams.Count, vm.Message);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, vm.Teams.Select(t => t.Mitglieder).ToArray());
            Assert.IsTrue(vm.Mitglieder.All(m => m.TeamId != null), "Nach dem Auslosen ist jemand ohne Team.");
            Assert.AreEqual(0, (await TeamsInDerDatenbankAsync()).Count, "Das Auslosen hat schon geschrieben - es ist ein Entwurf.");

            await vm.UebernehmenAsync();

            Assert.IsTrue(vm.Confirmed);
            Assert.AreEqual(2, (await TeamsInDerDatenbankAsync()).Count);
            Assert.AreEqual(2, world.Game.GameTeams.Count, "Das geladene Spiel kennt die neuen Teams nicht.");

            foreach (var mitglied in vm.Mitglieder)
                Assert.AreEqual(mitglied.TeamId, Teamzuordnung.TeamVon(world.Game, mitglied.PlayerId), $"{mitglied.Name}: das Spiel stempelt nicht das neue Team.");
        }

        [TestMethod]
        public void AnImpossibleDrawExplainsItselfAndChangesNothing()
        {
            var vm = Dialog();
            vm.AuslosAnzahl = 6;

            vm.DrawCommand.Execute(null);

            StringAssert.Contains(vm.Message, "mindestens 6 Mitspieler");
            Assert.AreEqual(0, vm.Teams.Count);
        }

        [TestMethod]
        public void AddingStopsAtEight()
        {
            var vm = Dialog();

            for (var i = 0; i < 9; i++)
                vm.AddTeamCommand.Execute(null);

            Assert.AreEqual(TeamGrenzen.MaxTeams, vm.Teams.Count);
            Assert.AreEqual(TeamGrenzen.MaxTeams, vm.Teams.Select(t => t.Farbe.Index).Distinct().Count(), "Zwei Teams tragen dieselbe Farbe.");
            StringAssert.Contains(vm.Message, "Höchstens 8 Teams");
        }

        [TestMethod]
        public async Task RemovingATeamLeavesItsMembersWithoutTeam()
        {
            var vm = Dialog();
            vm.AuslosAnzahl = 2;
            vm.DrawCommand.Execute(null);
            var weg = vm.Teams[0];
            var betroffen = vm.Mitglieder.Where(m => m.Auswahl == weg).ToList();

            await ((AsyncRelayCommand)vm.RemoveTeamCommand).ExecuteAsync(weg);

            Assert.AreEqual(1, vm.Teams.Count);
            Assert.IsFalse(vm.TeamAuswahl.Contains(weg), "Das entfernte Team steht noch zur Wahl.");
            Assert.IsTrue(betroffen.Count > 0 && betroffen.All(m => m.TeamId == null), "Die Mitglieder des entfernten Teams hängen noch daran.");
        }

        /// <summary>A10: ein Team mit Punkten lässt sich nicht entfernen - ein Satz im Dialog, das Team bleibt.</summary>
        [TestMethod]
        public async Task ATeamWithPointsCannotBeRemoved()
        {
            var vm = Dialog();
            vm.AuslosAnzahl = 2;
            vm.DrawCommand.Execute(null);
            await vm.UebernehmenAsync();
            var team = world.Game.GameTeams[0];

            using (var ctrl = new QuestionResultsController())
            {
                await ctrl.InsertAsync(new QuestionResult
                {
                    Id = Guid.NewGuid(), GameId = world.Game.Id, PlayerId = world.Players[0].Id, GameGridCoordinateId = world.Coordinate.Id,
                    QuestionBaseId = world.Question.Id, GameTeamId = team.Id,
                });
                await ctrl.SaveChangesAsync();
            }

            var zweiter = Dialog();
            await ((AsyncRelayCommand)zweiter.RemoveTeamCommand).ExecuteAsync(zweiter.Teams.Single(t => t.Id == team.Id));

            StringAssert.Contains(zweiter.Message, "nur noch umbenennen");
            Assert.AreEqual(2, zweiter.Teams.Count, "Das Team mit Punkten ist aus dem Entwurf verschwunden.");
        }

        /// <summary>A10: ein leerer Name wird gemeldet, nicht geworfen - der Dialog bleibt offen, nichts ist geschrieben.</summary>
        [TestMethod]
        public async Task AnInvalidDraftIsReportedNotThrown()
        {
            var vm = Dialog();
            vm.AddTeamCommand.Execute(null);
            vm.AddTeamCommand.Execute(null);
            vm.Teams[1].Name = "   ";

            await vm.UebernehmenAsync();

            Assert.IsFalse(vm.Confirmed);
            StringAssert.Contains(prompt.Informs.Single().Message, "braucht einen Namen");
            Assert.AreEqual(0, (await TeamsInDerDatenbankAsync()).Count);
            Assert.AreEqual(0, world.Game.GameTeams.Count, "Das geladene Spiel hat den abgewiesenen Entwurf übernommen.");
        }
    }
}
