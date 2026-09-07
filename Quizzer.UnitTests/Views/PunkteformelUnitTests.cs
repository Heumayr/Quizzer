using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Phasenteil der Punkteformel im Spielaufbau.
    /// <para>
    /// <c>CurrentPoints = Schwierigkeitsteil · (PhaseMultiplier · Phase) + PhaseAddition · Phase</c>
    /// - stehen beide Phasenwerte auf 0, ist das Ergebnis <b>immer</b> 0. In jeder Phase, für
    /// Plus wie Minus, für jede Zelle.
    /// </para>
    /// <para>
    /// <b>Gemessen 2026-09-07:</b> beide Felder liessen sich auf 0 setzen und wortlos speichern.
    /// Die Prüfregel der Maske erlaubt 0 ausdrücklich („Must be &gt;= 0"), und für jeden einzelnen
    /// Wert ist das auch richtig - erst die Kombination ist die Falle.
    /// </para>
    /// </summary>
    [TestClass]
    public class PunkteformelUnitTests
    {
        private TestGameBuilder world = null!;
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Beide Phasenwerte auf 0 wird abgewiesen und begründet.</summary>
        [TestMethod]
        public async Task BothPhaseValuesAtZeroAreRefused()
        {
            var vm = new EditGameViewModel();

            await vm.LoadModel(world.Game.Id);

            vm.Designation = "Umbenannt-" + Guid.NewGuid().ToString("N")[..8];
            vm.PhaseMultiplier = 0;
            vm.PhaseAddition = 0;

            await vm.VMSaveAsync();

            Assert.AreEqual(1, prompt.Informs.Count,
                "Es wurde nichts gesagt - jede Kachel waere danach 0 Punkte wert. Gelesen "
                + "wurde: " + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            StringAssert.Contains(prompt.Informs[0].Message, "0 Punkte",
                "Die Meldung nennt die Folge nicht: " + prompt.Informs[0].Message);

            // Und es wurde wirklich nichts geschrieben - eine Meldung allein genuegt nicht,
            // wenn danach trotzdem gespeichert wird.
            using var ctrl = new GamesController();

            var nachher = await ctrl.GetAsync(world.Game.Id);

            Assert.AreNotEqual(vm.Designation, nachher!.Designation,
                "Gemeldet wurde, gespeichert aber trotzdem.");

            Assert.AreEqual(1d, nachher.PhaseMultiplier,
                "Der Nullfaktor steht in der Datenbank.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung, und sie ist hier wesentlich.</b> Ein <i>einzelner</i> Nullwert
        /// ist zulässig: wer den Faktor auf 0 und den Zuschlag auf 100 stellt, will eine feste
        /// Punktzahl je Zelle statt der Punkte der Frage. Wäre das ebenfalls abgewiesen, nähme
        /// die Prüfung dem Spielleiter eine gültige Einstellung weg.
        /// </summary>
        [TestMethod]
        public async Task ASingleZeroIsStillAllowed()
        {
            var vm = new EditGameViewModel();

            await vm.LoadModel(world.Game.Id);

            vm.PhaseMultiplier = 0;
            vm.PhaseAddition = 100;

            await vm.VMSaveAsync();

            Assert.AreEqual(0, prompt.Informs.Count,
                "Eine gueltige Einstellung wurde abgewiesen: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            using var ctrl = new GamesController();

            var nachher = await ctrl.GetAsync(world.Game.Id);

            Assert.AreEqual(100, nachher!.PhaseAddition, "Gespeichert wurde nichts.");
        }
    }
}
