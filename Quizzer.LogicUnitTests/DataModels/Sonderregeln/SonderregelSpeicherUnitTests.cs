using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Wo die Sonderregel einer Zelle (M35 C3) ueberall mitgehen muss: Migration (Bestand: keine Regel,
    /// verdeckt), Klon, Buendel-Export in beide Richtungen und das Abgleichformat.
    /// </summary>
    [TestClass]
    public class SonderregelSpeicherUnitTests
    {
        /// <summary>Bestandszeilen bekommen keine Regel und bleiben verdeckt - die Operation zaehlt, nicht das Modell.</summary>
        [TestMethod]
        public void ExistingCellsGetNoRuleAndStayHiddenAfterTheMigration()
        {
            var migration = new Quizzer.Logic.Migrations.M35_Sonderregeln { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
            var spalten = migration.UpOperations.OfType<AddColumnOperation>().ToDictionary(o => o.Name);

            Assert.AreEqual(0, spalten[nameof(GameGridCoordinate.SpecialRule)].DefaultValue, "Bestandszeilen bekommen eine Regel.");
            Assert.AreEqual(false, spalten[nameof(GameGridCoordinate.SpecialRuleAnnounced)].DefaultValue, "Bestandszeilen sind angekuendigt.");
            Assert.IsTrue(spalten.Values.All(s => s.Table == nameof(GameGridCoordinate) && !s.IsNullable));
        }

        [TestMethod]
        public void TheCloneKeepsRuleAndAnnouncement()
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), SpecialRule = CellSpecialRule.Risk, SpecialRuleAnnounced = true };

            var klon = zelle.CloneWithoutReferences();

            Assert.AreEqual(CellSpecialRule.Risk, klon.SpecialRule, "Der Klon vergisst die Regel - sie wuerde nie gespeichert.");
            Assert.IsTrue(klon.SpecialRuleAnnounced, "Der Klon vergisst die Ankuendigung.");
        }

        /// <summary>Am Dokument und am Ergebnis geprueft: ein vergessenes Feld fiele am Ende sonst als Vorgabe nicht auf.</summary>
        [TestMethod]
        [DataRow(CellSpecialRule.Stake, true)]
        [DataRow(CellSpecialRule.Risk, false)]
        public void TheRuleSurvivesTheBundleRoundTrip(CellSpecialRule regel, bool angekuendigt)
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Probe" };
            var frage = new DefaultQuestion { Id = Guid.NewGuid(), CategoryId = kategorie.Id, Designation = "X" };
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(), QuestionBaseId = frage.Id, SpecialRule = regel, SpecialRuleAnnounced = angekuendigt,
            });

            var dokument = GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null);
            Assert.AreEqual(regel, dokument.Zellen.Single().SpecialRule, "Die Regel fehlt im Dokument.");

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(GameExportSerializer.ToJson(dokument)));
            var zelle = GameExportMapper.ToEntities(GameExportSerializer.Read(strom), _ => "datei.png").Zellen.Single().Zelle;

            Assert.AreEqual(regel, zelle.SpecialRule, "Die Regel kommt beim Import nicht an.");
            Assert.AreEqual(angekuendigt, zelle.SpecialRuleAnnounced, "Die Ankuendigung kommt beim Import nicht an.");
        }

        [TestMethod]
        public void ABundleWithAnUnknownRuleIsRejected()
        {
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid() });
            var dokument = GameExportMapper.ToDocument(spiel, [], [], null, _ => null);

            GameExportSerializer.Pruefe(dokument);
            dokument.Zellen.Single().SpecialRule = (CellSpecialRule)99;

            Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
        }

        /// <summary>
        /// Je Regel das Abgleichformat, mit dem sie kam. Ein altes WPF kennt eine neue Regel nicht und spielte
        /// die Zelle ohne sie - also neue Regel, neues Format. Eine Regel ohne Eintrag wird hier rot.
        /// </summary>
        [TestMethod]
        public void EverySpecialRuleHasAFormatThatIsNotAhead()
        {
            var seitFormat = new Dictionary<CellSpecialRule, int>
            {
                [CellSpecialRule.None] = 7,
                [CellSpecialRule.Stake] = 7,
                [CellSpecialRule.Risk] = 7,
            };

            var ohne = Enum.GetValues<CellSpecialRule>().Where(r => !seitFormat.ContainsKey(r)).ToList();

            Assert.AreEqual(0, ohne.Count, "Neue Sonderregel ohne Abgleichformat - SyncSnapshot.CurrentFormatVersion "
                + "hochzaehlen und hier eintragen: " + string.Join(", ", ohne));
            Assert.IsTrue(seitFormat.Values.Max() <= SyncSnapshot.CurrentFormatVersion,
                "Die Spalten der Sonderregel wandern im Abgleich, das Format steht noch davor.");
        }
    }
}
