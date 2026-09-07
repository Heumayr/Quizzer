using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Das Feld „Phase zur Ansicht" im Spielaufbau und der Knopf „Raster neu aufbauen".
    /// <para>
    /// <b>Gemessen 2026-09-07:</b> das Feld hatte keine Vorbelegung und zeigte <b>0</b>. Ein
    /// Klick auf „Raster neu aufbauen" setzte damit jede Kachel auf „0 / −0 Punkte" - der
    /// Phasenteil der Punkteformel multipliziert mit <c>(Faktor · Phase)</c> und nimmt bei
    /// Phase 0 alles weg. Ein anschließendes Speichern schrieb die Nullen fest.
    /// </para>
    /// </summary>
    [TestClass]
    public class RasteransichtUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Die Phase zur Ansicht beginnt bei 1 und lässt sich nicht darunter setzen.</summary>
        [TestMethod]
        public void ThePreviewPhaseNeverDropsBelowOne()
        {
            var vm = new EditGameViewModel();

            Assert.AreEqual(1, vm.TestPhase,
                "Das Feld beginnt bei 0 - ein Klick auf 'Raster neu aufbauen' nullt damit jede "
                + "Kachel, ohne dass der Spielleiter etwas eingegeben hat.");

            vm.TestPhase = 0;

            Assert.AreEqual(1, vm.TestPhase, "Eine 0 kommt durch und nullt die Punkte.");

            vm.TestPhase = -5;

            Assert.AreEqual(1, vm.TestPhase, "Ein negativer Wert kommt durch.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Eine echte Phase kommt durch - sonst ließe sich die Ansicht
        /// gar nicht mehr umstellen, und das Feld wäre sinnlos.
        /// </summary>
        [TestMethod]
        public void ARealPhaseIsAccepted()
        {
            var vm = new EditGameViewModel { TestPhase = 3 };

            Assert.AreEqual(3, vm.TestPhase, "Die eingestellte Phase kommt nicht an.");
        }

        /// <summary>
        /// <b>Eine gespielte Zelle behält ihre eingefrorenen Punkte.</b>
        /// <para>
        /// Der Rasteraufbau setzte die Phase bis 2026-09-07 direkt auf das Feld und umging damit
        /// den Riegel in <c>SetPhase</c> - eine bereits gespielte Zelle bekam neue Punkte,
        /// obwohl ihre längst vergeben sind.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task APlayedCellKeepsItsFrozenPoints()
        {
            var vm = new EditGameViewModel();

            await vm.LoadModel(world.Game.Id);

            var zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = world.Game.Id,
                X = 7,
                Y = 7,
                QuestionBaseId = world.Question.Id,
                QuestionBase = world.Question,
                Phase = 1,
                IsDone = true,
            };

            zelle.CalculateAndSetCurrentPoints();

            var eingefroren = zelle.CurrentPoints;

            zelle.SetPhase(5);

            Assert.AreEqual(eingefroren, zelle.CurrentPoints,
                "Die Punkte einer gespielten Zelle haben sich geaendert - der Punktestand des "
                + "Abends verschiebt sich nachtraeglich.");
        }
    }
}
