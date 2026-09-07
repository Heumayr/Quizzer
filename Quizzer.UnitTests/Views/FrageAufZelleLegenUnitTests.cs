using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.HelperViewModels;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Eine Frage auf eine Zelle legen — die Kernhandlung des Spielaufbaus.
    /// <para>
    /// <b>Sie hatte bis 2026-09-07 keine einzige Zusicherung.</b> Aufgefallen ist das bei einer
    /// Durchzählung der ViewModels ohne Test; vier standen ohne da, und dies ist das
    /// folgenreichste: bricht es, lässt sich kein Spielfeld mehr bauen.
    /// </para>
    /// </summary>
    [TestClass]
    public class FrageAufZelleLegenUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private QuestionSelectorViewModel Aufbau(GameGridCoordinate zelle)
        {
            var vm = new QuestionSelectorViewModel();

            vm.SetDependencys(world.Game, zelle);

            return vm;
        }

        /// <summary>
        /// <b>Die Auswahl setzt beide Hälften.</b> Nur die Kennung zu setzen genügt nicht: die
        /// Punkte einer Zelle rechnen sich aus <c>QuestionBase</c>, und ohne den Rückverweis
        /// bleibt der gespeicherte Stand stehen — im schlimmsten Fall eine 0.
        /// </summary>
        [TestMethod]
        public void ChoosingAQuestionSetsBothHalves()
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = world.Game.Id };

            var vm = Aufbau(zelle);

            vm.SelectedQuestion = world.Question;

            Assert.AreEqual(world.Question.Id, zelle.QuestionBaseId,
                "Die Kennung der Frage kam nicht an der Zelle an.");

            Assert.AreSame(world.Question, zelle.QuestionBase,
                "Der Rueckverweis auf die Frage fehlt - ohne ihn rechnet "
                + "CalculateAndSetCurrentPoints gar nicht und laesst den alten Stand stehen.");
        }

        /// <summary>
        /// <b>Die Auswahl zu entfernen setzt die Kennung auf <c>null</c>, nicht auf
        /// <c>Guid.Empty</c>.</b>
        /// <para>
        /// Der Unterschied ist gemessen und stand als Kommentar im Code, aber ohne Zusicherung:
        /// der Aufrufer prüft auf <c>HasValue</c>. Wäre es <c>Guid.Empty</c>, spränge er an
        /// seinem Nachladezweig vorbei, und die geleerte Zelle zeigte weiter ihre alten Punkte.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ClearingTheChoiceYieldsNullNotEmpty()
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = world.Game.Id };

            var vm = Aufbau(zelle);

            vm.SelectedQuestion = world.Question;
            vm.SelectedQuestion = null;

            Assert.IsNull(zelle.QuestionBaseId,
                "Nach dem Entfernen steht Guid.Empty statt null - der Aufrufer prueft auf "
                + "HasValue und laedt dann nicht nach.");

            Assert.IsNull(zelle.QuestionBase, "Der Rueckverweis blieb stehen.");

            Assert.IsFalse(zelle.HatFrage,
                "Die Zelle gilt weiter als belegt - sie zaehlte dann fuer Fortschritt und "
                + "Phasenschwelle mit, obwohl sie leer ist.");
        }

        /// <summary>
        /// <b>Eine Frage, die schon auf einer anderen Zelle liegt, wird nicht mehr angeboten.</b>
        /// Sonst stünde dieselbe Frage zweimal im Spielfeld.
        /// </summary>
        [TestMethod]
        public void AQuestionAlreadyOnTheGridIsNotOfferedAgain()
        {
            // world.Coordinate traegt die Frage bereits.
            var zweite = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = world.Game.Id };

            var vm = Aufbau(zweite);

            vm.AllQuestion = [world.Question];

            vm.CalculateAvailableQuestions();

            Assert.AreEqual(0, vm.AvailableQuestions.Count,
                "Die schon vergebene Frage wird erneut angeboten - sie stuende dann zweimal im "
                + "Spielfeld.");

            // Die Gegenrichtung: eine freie Frage kommt durch. Ohne sie waere die Zusicherung
            // auch gruen, wenn gar nichts mehr angeboten wuerde.
            var frei = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Noch frei" };

            vm.AllQuestion = [world.Question, frei];

            vm.CalculateAvailableQuestions();

            Assert.AreEqual(1, vm.AvailableQuestions.Count,
                "Eine freie Frage wird nicht angeboten - dann laesst sich gar nichts mehr "
                + "zuweisen.");

            Assert.AreEqual(frei.Id, vm.AvailableQuestions[0].Id);
        }

        /// <summary>
        /// Die Maske ist deutsch. Bis 2026-09-07 stand dort <c>„No question selected"</c>.
        /// </summary>
        [TestMethod]
        public void TheEmptySelectionSaysItInGerman()
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = world.Game.Id };

            var vm = Aufbau(zelle);

            Assert.AreEqual("Keine Frage ausgewählt", vm.CurrentSelectedQuestionDisplay,
                "Der Text neben der Auswahl ist nicht deutsch.");

            vm.SelectedQuestion = world.Question;

            StringAssert.Contains(vm.CurrentSelectedQuestionDisplay, world.Question.Designation,
                "Nach der Auswahl steht die Frage nicht da: " + vm.CurrentSelectedQuestionDisplay);
        }
    }
}
