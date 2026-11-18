using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Raster und Phasen im Bündel (M37 P7, P5-4): nie negativ - wie die Maske (<c>PositiveIntValidationRule</c>). Eine negative
    /// Zellengröße warf im Spielfeld; 0 bleibt erlaubt, Spiele von vor der Phasenzahl tragen dort 0. Muster: <see cref="GameExportPunkteUnitTests"/>.
    /// </summary>
    [TestClass]
    public class GameExportRasterUnitTests
    {
        private static GameExportDocument Dokument()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Kat" };
            var frage = new Quizzer.DataModels.Models.QuestionTypes.DefaultQuestion
            {
                Id = Guid.NewGuid(), Designation = "Frage", QuestionText = "Wie?", CategoryId = kategorie.Id, Points = 100,
            };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Abend" };
            return GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null);
        }

        private static void Setze(GameExportDocument dokument, string feld, int wert)
        {
            var eigenschaft = typeof(GameExportDocument.GameData).GetProperty(feld) ?? throw new AssertFailedException($"{feld} gibt es im Bündel nicht.");
            eigenschaft.SetValue(dokument.Spiel, Convert.ChangeType(wert, eigenschaft.PropertyType));
        }

        [TestMethod]
        [DataRow(nameof(GameExportDocument.GameData.Height))]
        [DataRow(nameof(GameExportDocument.GameData.Width))]
        [DataRow(nameof(GameExportDocument.GameData.CellHeight))]
        [DataRow(nameof(GameExportDocument.GameData.CellWidth))]
        [DataRow(nameof(GameExportDocument.GameData.SuggestedPhases))]
        public void ANegativeGridValueIsRejectedZeroIsNot(string feld)
        {
            var null_ = Dokument();
            Setze(null_, feld, 0);
            GameExportSerializer.Pruefe(null_);

            var negativ = Dokument();
            Setze(negativ, feld, -1);
            var fehler = Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(negativ));
            StringAssert.Contains(fehler.Message, "negativ");
        }
    }
}
