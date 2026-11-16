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

        /// <summary>Anteil und Spielweise (M35 C3b): Bestand 0/0 - also Vorgabe des Profils und erste Spielweise.</summary>
        [TestMethod]
        public void ExistingCellsGetTheDefaultShareAndTheFirstVariantAfterTheMigration()
        {
            var migration = new Quizzer.Logic.Migrations.M35_SonderregelAnteil { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
            var spalten = migration.UpOperations.OfType<AddColumnOperation>().ToDictionary(o => o.Name);

            Assert.AreEqual(2, spalten.Count, "Die Migration bringt mehr oder weniger als Anteil und Spielweise.");
            Assert.AreEqual(0, spalten[nameof(GameGridCoordinate.SpecialRuleShare)].DefaultValue, "Bestandszeilen bekommen einen eigenen Anteil.");
            Assert.AreEqual(0, spalten[nameof(GameGridCoordinate.SpecialRuleVariant)].DefaultValue, "Bestandszeilen bekommen eine andere Spielweise.");
            Assert.IsTrue(spalten.Values.All(s => s.Table == nameof(GameGridCoordinate) && !s.IsNullable));
        }

        [TestMethod]
        public void TheCloneKeepsRuleAndAnnouncement()
        {
            var zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(), SpecialRule = CellSpecialRule.Risk, SpecialRuleAnnounced = true, SpecialRuleShare = 35, SpecialRuleVariant = 1,
            };

            var klon = zelle.CloneWithoutReferences();

            Assert.AreEqual(CellSpecialRule.Risk, klon.SpecialRule, "Der Klon vergisst die Regel - sie wuerde nie gespeichert.");
            Assert.IsTrue(klon.SpecialRuleAnnounced, "Der Klon vergisst die Ankuendigung.");
            Assert.AreEqual(35, klon.SpecialRuleShare, "Der Klon vergisst den Anteil.");
            Assert.AreEqual(1, klon.SpecialRuleVariant, "Der Klon vergisst die Spielweise.");
        }

        /// <summary>Am Dokument und am Ergebnis geprueft: ein vergessenes Feld fiele am Ende sonst als Vorgabe nicht auf.</summary>
        [TestMethod]
        [DataRow(CellSpecialRule.Stake, true, 25, 1)]
        [DataRow(CellSpecialRule.Risk, false, 0, 0)]
        public void TheRuleSurvivesTheBundleRoundTrip(CellSpecialRule regel, bool angekuendigt, int anteil, int spielweise)
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Probe" };
            var frage = new DefaultQuestion { Id = Guid.NewGuid(), CategoryId = kategorie.Id, Designation = "X" };
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(), QuestionBaseId = frage.Id, SpecialRule = regel, SpecialRuleAnnounced = angekuendigt,
                SpecialRuleShare = anteil, SpecialRuleVariant = spielweise,
            });

            var dokument = GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null);
            Assert.AreEqual(regel, dokument.Zellen.Single().SpecialRule, "Die Regel fehlt im Dokument.");
            Assert.AreEqual(anteil, dokument.Zellen.Single().SpecialRuleShare, "Der Anteil fehlt im Dokument.");
            Assert.AreEqual(spielweise, dokument.Zellen.Single().SpecialRuleVariant, "Die Spielweise fehlt im Dokument.");

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(GameExportSerializer.ToJson(dokument)));
            var zelle = GameExportMapper.ToEntities(GameExportSerializer.Read(strom), _ => "datei.png").Zellen.Single().Zelle;

            Assert.AreEqual(regel, zelle.SpecialRule, "Die Regel kommt beim Import nicht an.");
            Assert.AreEqual(angekuendigt, zelle.SpecialRuleAnnounced, "Die Ankuendigung kommt beim Import nicht an.");
            Assert.AreEqual(anteil, zelle.SpecialRuleShare, "Der Anteil kommt beim Import nicht an.");
            Assert.AreEqual(spielweise, zelle.SpecialRuleVariant, "Die Spielweise kommt beim Import nicht an.");
        }

        [TestMethod]
        [DataRow(-1, 0)]
        [DataRow(101, 0)]
        [DataRow(0, -1)]
        public void ABundleWithAnImpossibleShareOrVariantIsRejected(int anteil, int spielweise)
        {
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), SpecialRule = CellSpecialRule.Stake });
            var dokument = GameExportMapper.ToDocument(spiel, [], [], null, _ => null);

            GameExportSerializer.Pruefe(dokument);
            dokument.Zellen.Single().SpecialRuleShare = anteil;
            dokument.Zellen.Single().SpecialRuleVariant = spielweise;

            Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
        }

        /// <summary>Die Grenze selbst geht durch - sonst wuerde 100 % abgewiesen.</summary>
        [TestMethod]
        public void ABundleWithTheFullShareIsAccepted()
        {
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), SpecialRule = CellSpecialRule.Stake, SpecialRuleShare = 100 });

            GameExportSerializer.Pruefe(GameExportMapper.ToDocument(spiel, [], [], null, _ => null));
        }

        /// <summary>Anteil und Spielweise wandern im Abgleich (Spalten) - ein altes WPF setzte sie still auf 0.</summary>
        [TestMethod]
        public void ShareAndVariantCameWithFormat8()
        {
            if (SyncSnapshot.CurrentFormatVersion < 8)
                Assert.Fail("Neue Spalten ohne neues Abgleichformat.");
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
                [CellSpecialRule.Auction] = 9,
                [CellSpecialRule.Bet] = 10,
                [CellSpecialRule.Expert] = 11,
            };

            var ohne = Enum.GetValues<CellSpecialRule>().Where(r => !seitFormat.ContainsKey(r)).ToList();

            Assert.AreEqual(0, ohne.Count, "Neue Sonderregel ohne Abgleichformat - SyncSnapshot.CurrentFormatVersion "
                + "hochzaehlen und hier eintragen: " + string.Join(", ", ohne));
            Assert.IsTrue(seitFormat.Values.Max() <= SyncSnapshot.CurrentFormatVersion,
                "Die Spalten der Sonderregel wandern im Abgleich, das Format steht noch davor.");
        }
    }
}
