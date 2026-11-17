using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.HelperViewModels;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Das Finale im Spielaufbau (M37 E4): es lässt sich einer Zelle geben, mit und ohne Teams - aber nur einer je Spiel. Trägt
    /// schon eine andere Zelle das Finale, bietet der Auswahldialog es nicht an und nimmt es nicht; liegen zwei (aus Bündel oder
    /// Abgleich), sagt der Hinweis es.
    /// </summary>
    [TestClass]
    public class FinaleAufbauUnitTests
    {
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
    }
}
