using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Punkte-Parameter im Bündel (M37 P5, R3-3): nie negativ - wie die Maske (<c>PositiveIntValidationRule</c>,
    /// <c>PositiveDoubleValidationRule</c>). Ein negativer Faktor machte den Feldwert negativ, und eine Teamregel teilte Minus aus.
    /// </summary>
    [TestClass]
    public class GameExportPunkteUnitTests
    {
        private static GameExportDocument Dokument()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Kat" };
            var frage = new Quizzer.DataModels.Models.QuestionTypes.DefaultQuestion
            {
                Id = Guid.NewGuid(), Designation = "Frage", QuestionText = "Wie?", CategoryId = kategorie.Id, Points = 100, MinusPoints = 50,
            };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Abend" };
            return GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null);
        }

        [TestMethod]
        public void ZeroAndPositiveValuesPass()
        {
            var dokument = Dokument();
            dokument.Spiel.PhaseAddition = 0;
            dokument.Spiel.DifficultyMultiplier = 0;

            GameExportSerializer.Pruefe(dokument);
        }

        [TestMethod]
        [DataRow(nameof(GameExportDocument.GameData.DifficultyMultiplier))]
        [DataRow(nameof(GameExportDocument.GameData.DifficultyAddition))]
        [DataRow(nameof(GameExportDocument.GameData.DifficultyMinusMultiplier))]
        [DataRow(nameof(GameExportDocument.GameData.DifficultyMinusAddition))]
        [DataRow(nameof(GameExportDocument.GameData.PhaseMultiplier))]
        [DataRow(nameof(GameExportDocument.GameData.PhaseAddition))]
        public void ANegativeGameParameterIsRejected(string feld)
        {
            var dokument = Dokument();
            var eigenschaft = typeof(GameExportDocument.GameData).GetProperty(feld) ?? throw new AssertFailedException($"{feld} gibt es im Bündel nicht.");
            eigenschaft.SetValue(dokument.Spiel, Convert.ChangeType(-1, eigenschaft.PropertyType));

            var fehler = Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
            StringAssert.Contains(fehler.Message, "negativ");
        }

        [TestMethod]
        [DataRow(-1, 50, DisplayName = "Punkte")]
        [DataRow(100, -1, DisplayName = "Minuspunkte")]
        public void ANegativeQuestionValueIsRejected(int punkte, int minus)
        {
            var dokument = Dokument();
            dokument.Fragen.Single().Points = punkte;
            dokument.Fragen.Single().MinusPoints = minus;

            var fehler = Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
            StringAssert.Contains(fehler.Message, "negativ");
        }
    }
}
