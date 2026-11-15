using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>M35 B1: Kontostand und die Einstellung „Kontostand darf unter 0 fallen" (E2).</summary>
    [TestClass]
    public class PunktebewegungUnitTests
    {
        [TestMethod]
        [DataRow(-50, 0, true, -50, DisplayName = "erlaubt: Minus bleibt")]
        [DataRow(80, 0, false, 80, DisplayName = "Plus bleibt immer")]
        [DataRow(-50, 200, false, -50, DisplayName = "Minus kleiner als Kontostand bleibt")]
        [DataRow(-50, 30, false, -30, DisplayName = "Minus groesser als Kontostand wird gekappt")]
        [DataRow(-50, 0, false, 0, DisplayName = "Kontostand 0: nichts abziehen")]
        [DataRow(-50, -20, false, 0, DisplayName = "Kontostand schon unter 0: nichts abziehen")]
        public void AMinusIsCappedOnlyWhenTheGameForbidsIt(int vorschlag, int kontostand, bool erlaubt, int erwartet)
        {
            Assert.AreEqual(erwartet, Punktebewegung.Kappe(vorschlag, kontostand, erlaubt));
        }

        [TestMethod]
        public void TheBalanceSumsOnlyThePlayersOwnResultsAcrossAllCells()
        {
            var anna = Guid.NewGuid();
            var ben = Guid.NewGuid();
            var spiel = new Game();
            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                QuestionResults = [new QuestionResult { PlayerId = anna, Score = 200 }, new QuestionResult { PlayerId = ben, MinusScore = 50 }],
            });
            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                QuestionResults = [new QuestionResult { PlayerId = anna, Correction = -150 }, new QuestionResult { PlayerId = ben, Correction = 150 }],
            });

            Assert.AreEqual(50, spiel.Kontostand(anna));
            Assert.AreEqual(100, spiel.Kontostand(ben));
        }

        [TestMethod]
        public void ANewGameAllowsANegativeBalanceLikeBefore()
        {
            Assert.IsTrue(new Game().AllowNegativeScore);
        }
    }
}
