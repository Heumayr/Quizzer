using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.DataModels.Models.Base
{
    /// <summary>
    /// Phasen und Punkteschwellen. Die Schwellen bestimmen, wann das Spiel den
    /// Phasenwechsel vorschlaegt.
    /// </summary>
    [TestClass]
    public class GameUnitTests
    {
        private static Game GameWith(int coordinateCount, int suggestedPhases)
        {
            var game = new Game { SuggestedPhases = suggestedPhases };

            for (var i = 0; i < coordinateCount; i++)
                game.GameGridCoordinates.Add(new GameGridCoordinate { Game = game, X = i, Y = 0 });

            return game;
        }

        [TestMethod]
        public void CalculatetThreshold_SplitsTheGridEvenly()
        {
            var game = GameWith(coordinateCount: 12, suggestedPhases: 3);

            game.CalculatetThreshold();

            CollectionAssert.AreEqual(new[] { 4, 8 }, game.PhaseTrashholds.ToArray(),
                "Bei drei Phasen liegen zwei Schwellen dazwischen, nicht drei.");
        }

        [TestMethod]
        public void CalculatetThreshold_NeverPlacesAThresholdOnTheLastCell()
        {
            var game = GameWith(coordinateCount: 10, suggestedPhases: 2);

            game.CalculatetThreshold();

            CollectionAssert.DoesNotContain(game.PhaseTrashholds.ToArray(), 10);
        }

        [TestMethod]
        public void CalculatetThreshold_WithUnevenSplit_RoundsUp()
        {
            // 10 Zellen auf 3 Phasen: Spanne 4 -> Schwellen bei 4 und 8
            var game = GameWith(coordinateCount: 10, suggestedPhases: 3);

            game.CalculatetThreshold();

            CollectionAssert.AreEqual(new[] { 4, 8 }, game.PhaseTrashholds.ToArray());
        }

        [TestMethod]
        public void CalculatetThreshold_WithoutCoordinates_YieldsNoThresholds()
        {
            var game = GameWith(coordinateCount: 0, suggestedPhases: 3);

            game.CalculatetThreshold();

            Assert.AreEqual(0, game.PhaseTrashholds.Count);
        }

        [TestMethod]
        public void CalculatetThreshold_WithoutSuggestedPhases_YieldsNoThresholds()
        {
            var game = GameWith(coordinateCount: 12, suggestedPhases: 0);

            game.CalculatetThreshold();

            Assert.AreEqual(0, game.PhaseTrashholds.Count);
        }

        [TestMethod]
        public void CalculatetThreshold_IsRepeatable()
        {
            var game = GameWith(coordinateCount: 12, suggestedPhases: 3);

            game.CalculatetThreshold();
            game.CalculatetThreshold();

            CollectionAssert.AreEqual(new[] { 4, 8 }, game.PhaseTrashholds.ToArray(),
                "Zweimal gerechnet darf die Liste nicht doppelt so lang sein.");
        }

        [TestMethod]
        public void SetPhaseAndSetCoordinatesPhase_CarriesThePhaseIntoEveryCell()
        {
            var game = GameWith(coordinateCount: 4, suggestedPhases: 2);

            game.SetPhaseAndSetCoordinatesPhase(3);

            Assert.AreEqual(3, game.Phase);
            Assert.IsTrue(game.GameGridCoordinates.All(c => c.Phase == 3));
        }

        [TestMethod]
        public void SetPhaseAndSetCoordinatesPhase_NeverGoesBelowOne()
        {
            var game = GameWith(coordinateCount: 2, suggestedPhases: 2);

            game.SetPhaseAndSetCoordinatesPhase(-5);

            Assert.AreEqual(1, game.Phase);
        }

        [TestMethod]
        public void RaiseAndLowerPhase_AreEachOthersOpposite()
        {
            var game = GameWith(coordinateCount: 2, suggestedPhases: 2);

            game.RaisePhase();
            game.RaisePhase();
            game.LowerPhase();

            Assert.AreEqual(2, game.Phase);
        }
    }
}
