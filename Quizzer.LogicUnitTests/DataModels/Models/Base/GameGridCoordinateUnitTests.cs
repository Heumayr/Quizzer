using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.LogicUnitTests.DataModels.Models.Base
{
    /// <summary>
    /// Die Punkteformel einer Spielfeldzelle. Reine Rechnung - keine Oberflaeche,
    /// keine Datenbank.
    /// </summary>
    [TestClass]
    public class GameGridCoordinateUnitTests
    {
        private static GameGridCoordinate CoordinateWith(
            int points = 100,
            int minusPoints = 100,
            Difficulty difficulty = Difficulty.Level0,
            int phase = 1,
            double difficultyMultiplier = 1,
            int difficultyAddition = 0,
            double difficultyMinusMultiplier = 0.1,
            int difficultyMinusAddition = 0,
            double phaseMultiplier = 1,
            int phaseAddition = 0)
        {
            var game = new Game
            {
                DifficultyMultiplier = difficultyMultiplier,
                DifficultyAddition = difficultyAddition,
                DifficultyMinusMultiplier = difficultyMinusMultiplier,
                DifficultyMinusAddition = difficultyMinusAddition,
                PhaseMultiplier = phaseMultiplier,
                PhaseAddition = phaseAddition,
            };

            return new GameGridCoordinate
            {
                Game = game,
                Phase = phase,
                QuestionBase = new DefaultQuestion
                {
                    Points = points,
                    MinusPoints = minusPoints,
                    Difficulty = difficulty,
                },
            };
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_WithDefaults_KeepsBasePoints()
        {
            var coordinate = CoordinateWith(points: 100, difficulty: Difficulty.Level0, phase: 1);

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(100, coordinate.CurrentPoints);
        }

        [TestMethod]
        [DataRow(Difficulty.Level0, 100)]
        [DataRow(Difficulty.Level1, 200)]
        [DataRow(Difficulty.Level3, 400)]
        public void CalculateAndSetCurrentPoints_ScalesWithDifficulty(Difficulty difficulty, int expected)
        {
            // Points * (DifficultyMultiplier * (int)Difficulty + 1)
            var coordinate = CoordinateWith(points: 100, difficulty: difficulty, difficultyMultiplier: 1);

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(expected, coordinate.CurrentPoints);
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_AddsDifficultyAddition()
        {
            // 100 * (1 * 2 + 1) + (50 * 2) = 400
            var coordinate = CoordinateWith(
                points: 100, difficulty: Difficulty.Level2, difficultyAddition: 50);

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(400, coordinate.CurrentPoints);
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_ScalesWithPhase()
        {
            // (100 * 1) * (2 * 3) = 600 - die Phase multipliziert das Zwischenergebnis
            var coordinate = CoordinateWith(
                points: 100, difficulty: Difficulty.Level0, phase: 3, phaseMultiplier: 2);

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(600, coordinate.CurrentPoints);
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_MinusPointsUseTheirOwnMultiplier()
        {
            // 100 * (0.1 * 5 + 1) = 150
            var coordinate = CoordinateWith(
                minusPoints: 100, difficulty: Difficulty.Level5, difficultyMinusMultiplier: 0.1);

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(150, coordinate.CurrentMinusPoints);
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_TruncatesTowardsZero()
        {
            // 100 * (0.35 * 1 + 1) = 135.0 -> hier bewusst ein krummer Wert:
            // 10 * (0.33 * 1 + 1) = 13.3 -> 13, nicht 14
            var coordinate = CoordinateWith(
                points: 10, difficulty: Difficulty.Level1, difficultyMultiplier: 0.33);

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(13, coordinate.CurrentPoints);
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_WhenDone_LeavesPointsUntouched()
        {
            var coordinate = CoordinateWith(points: 100);
            coordinate.CalculateAndSetCurrentPoints();
            var before = coordinate.CurrentPoints;

            coordinate.IsDone = true;
            coordinate.QuestionBase!.Points = 999;
            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(before, coordinate.CurrentPoints,
                "Eine erledigte Zelle behaelt die Punkte, mit denen sie gespielt wurde.");
        }

        [TestMethod]
        public void CalculateAndSetCurrentPoints_WithoutQuestion_YieldsZero()
        {
            var coordinate = CoordinateWith();
            coordinate.QuestionBase = null;

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(0, coordinate.CurrentPoints);
            Assert.AreEqual(0, coordinate.CurrentMinusPoints);
        }

        [TestMethod]
        public void LowerPhase_NeverGoesBelowOne()
        {
            var coordinate = CoordinateWith(phase: 1);

            coordinate.LowerPhase();

            Assert.AreEqual(1, coordinate.Phase);
        }

        [TestMethod]
        public void RaisePhase_WhenDone_DoesNothing()
        {
            var coordinate = CoordinateWith(phase: 2);
            coordinate.IsDone = true;

            coordinate.RaisePhase();

            Assert.AreEqual(2, coordinate.Phase);
        }
        /// <summary>
        /// Die Punkte sind gespeicherte Spalten. Kann die Rechnung nicht rechnen, weil die
        /// Rueckverweise im Speicher fehlen, darf sie den gespeicherten Stand nicht ueberschreiben -
        /// sonst wird aus einer Zelle mit 600 Punkten still eine mit null.
        /// </summary>
        [TestMethod]
        public void WithoutBackReferences_TheStoredPointsAreKept()
        {
            var coordinate = new GameGridCoordinate
            {
                QuestionBaseId = Guid.NewGuid(),   // eine Frage ist zugewiesen ...
                QuestionBase = null,               // ... aber nicht geladen
                Game = null!,
                CurrentPoints = 600,
                CurrentMinusPoints = 165,
            };

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(600, coordinate.CurrentPoints,
                "Der gespeicherte Punktestand wurde ueberschrieben, obwohl nichts zu rechnen war.");
            Assert.AreEqual(165, coordinate.CurrentMinusPoints);
        }

        /// <summary>
        /// Die Gegenrichtung: ist wirklich keine Frage zugewiesen, gehoert die Zelle auf null.
        /// Ohne diese Probe wuerde ein pauschales "nichts anfassen" nicht auffallen.
        /// </summary>
        [TestMethod]
        public void WithoutAQuestion_ThePointsAreCleared()
        {
            var coordinate = new GameGridCoordinate
            {
                QuestionBaseId = null,
                QuestionBase = null,
                Game = new Game(),
                CurrentPoints = 600,
                CurrentMinusPoints = 165,
            };

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(0, coordinate.CurrentPoints,
                "Eine leere Zelle muss null Punkte tragen.");
            Assert.AreEqual(0, coordinate.CurrentMinusPoints);
        }

        /// <summary>Eine leere Kennung zaehlt wie keine.</summary>
        [TestMethod]
        public void AnEmptyQuestionIdCountsAsNoQuestion()
        {
            var coordinate = new GameGridCoordinate
            {
                QuestionBaseId = Guid.Empty,
                Game = new Game(),
                CurrentPoints = 600,
            };

            coordinate.CalculateAndSetCurrentPoints();

            Assert.AreEqual(0, coordinate.CurrentPoints);
        }
    }
}
