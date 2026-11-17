using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using Quizzer.Views.HelperViewModels;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Das Finale im Spielaufbau (M37 E4): es lässt sich einer Zelle geben, mit und ohne Teams - aber nur einer je Spiel. Trägt
    /// schon eine andere Zelle das Finale, bietet der Auswahldialog es nicht an und nimmt es nicht; liegen zwei (aus Bündel oder
    /// Abgleich), sagen es der Hinweis und der Spielstart.
    /// </summary>
    [TestClass]
    public class FinaleAufbauUnitTests
    {
        [TestCleanup]
        public void Cleanup() => UserPrompt.Reset();

        private static GameGridCoordinate Zelle(Game spiel, int x)
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), X = x, Game = spiel, GameId = spiel.Id, QuestionBase = new DefaultQuestion { Id = Guid.NewGuid() } };
            spiel.GameGridCoordinates.Add(zelle);
            return zelle;
        }

        private static readonly SonderregelOption Finale = new(CellSpecialRule.Final, "Finale");

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TheFinalCanBeGivenToOneCellOnly(bool teamspiel)
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = teamspiel };
            var letzte = Zelle(spiel, 0);
            var andere = Zelle(spiel, 1);
            var vm = new QuestionSelectorViewModel();

            vm.SetDependencys(spiel, letzte);
            CollectionAssert.Contains(vm.SonderregelOptionen.ToList(), Finale, "Das Finale wird nicht angeboten.");
            vm.GewaehlteSonderregel = Finale;
            Assert.AreEqual(CellSpecialRule.Final, letzte.SpecialRule);

            vm.SetDependencys(spiel, andere);
            CollectionAssert.DoesNotContain(vm.SonderregelOptionen.ToList(), Finale, "Ein zweites Finale wird angeboten.");
            vm.GewaehlteSonderregel = Finale;
            Assert.AreEqual(CellSpecialRule.None, andere.SpecialRule, "Ein zweites Finale ließ sich setzen.");

            // Die Zelle mit dem Finale behält es in der Auswahl; wer es dort wegnimmt, kann es woanders setzen.
            vm.SetDependencys(spiel, letzte);
            Assert.AreEqual(Finale, vm.GewaehlteSonderregel, "Die Zelle mit dem Finale zeigt es nicht als gewählt.");
            Assert.AreEqual(string.Empty, vm.SonderregelHinweis);
            vm.GewaehlteSonderregel = vm.SonderregelOptionen.Single(o => o.Regel == CellSpecialRule.None);

            vm.SetDependencys(spiel, andere);
            vm.GewaehlteSonderregel = Finale;
            Assert.AreEqual(CellSpecialRule.Final, andere.SpecialRule, "Nach dem Wegnehmen lässt sich das Finale nicht umsetzen.");
        }

        [TestMethod]
        public void TwoFinalsFromABundleAreNamedInTheHint()
        {
            var spiel = new Game { Id = Guid.NewGuid() };
            var eins = Zelle(spiel, 0);
            Zelle(spiel, 1).SpecialRule = CellSpecialRule.Final;
            eins.SpecialRule = CellSpecialRule.Final;
            var vm = new QuestionSelectorViewModel();

            vm.SetDependencys(spiel, eins);

            Assert.AreEqual(Finale, vm.GewaehlteSonderregel, "Ein doppeltes Finale verschwindet aus der Auswahl.");
            StringAssert.Contains(vm.SonderregelHinweis, "einmal je Spiel");
        }

        /// <summary>Ein zweites Finale hält den Start nicht an, wird aber gefragt - über den Weg, den der Start wirklich nimmt (E1).</summary>
        [TestMethod]
        public async Task ASecondFinalIsAskedAtTheGameStartNotRefused()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 2);
            var leiter = new Player { Id = Guid.NewGuid(), Designation = "Leo" };
            var prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            world.Game.Moderator = leiter;
            world.Game.ModeratorPlayerId = leiter.Id;
            world.Coordinate.SpecialRule = CellSpecialRule.Final;
            Assert.IsTrue(await GameMasterViewModel.StartbarAsync(world.Game));
            Assert.AreEqual(0, prompt.Confirms.Count, "Ein einzelnes Finale wird beim Start bemängelt.");

            world.Game.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), X = 3, Y = 1, GameId = world.Game.Id, SpecialRule = CellSpecialRule.Final });
            Assert.IsTrue(await GameMasterViewModel.StartbarAsync(world.Game));
            Assert.AreEqual(1, prompt.Confirms.Count, "Das zweite Finale wurde nicht gefragt.");
            StringAssert.Contains(prompt.Confirms[0].Message, "„Finale“ liegt 2-mal im Raster");
        }
    }
}
