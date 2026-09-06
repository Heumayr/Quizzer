using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Eine Standardfrage von vorne bis hinten - so, wie der Spielleiter sie spielt:
    /// oeffnen, Schritt fuer Schritt aufdecken, Aufloesung, schliessen.
    /// Ohne Fenster, ohne Buzzer, aber ueber dieselben ViewModels.
    /// </summary>
    [TestClass]
    public class DefaultQuestionPlayThroughUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static TestableCurrentQuestionViewModel OpenOn(TestGameBuilder world)
            => new() { Coordinate = world.Coordinate };

        [TestMethod]
        public async Task OpeningAQuestion_LoadsItsStepsAndPlayers()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 2);
            var vm = OpenOn(world);

            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.IsNotNull(vm.Coordinate?.QuestionBase);

            // Zwei ergaenzte Bildschirme (Start und Frage), zwei Hinweise, Aufloesung.
            var schritte = vm.Coordinate.QuestionBase.OrderedSteps;

            Assert.AreEqual(vm.Coordinate.QuestionBase.Steps.Count + 2, schritte.Length,
                "Es wurden nicht genau zwei Bildschirme ergaenzt - der Startschritt und der "
                + "Fragebildschirm.");

            Assert.IsTrue(schritte[0].IsStart,
                "Der erste Bildschirm ist kein Startschritt. Der Spielleiter liest vor, bevor "
                + "etwas auf dem Beamer steht.");

            Assert.IsTrue(schritte[1].IsQuestionOnly,
                "Der zweite Bildschirm zeigt nicht nur die Frage. Bei einer Schaetzfrage stuenden "
                + "damit Frage und Antwort zugleich da.");
            Assert.IsNotNull(vm.PlayersResultViewModel);
            Assert.AreEqual(2, vm.PlayersResultViewModel.Results.Count,
                "Fuer jeden Mitspieler ein Ergebnis.");
        }

        [TestMethod]
        public async Task SteppingForward_ReachesTheFinishStep()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 2);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();

            await vm.StartStepCommnad!.ExecuteAsync(null);
            var visited = new List<string?> { vm.CurrentStep?.Designation };
            var marken = new List<Quizzer.DataModels.Models.Base.QuestionStepResource> { vm.CurrentStep! };

            while (vm.NextStep != null)
            {
                await vm.NextStepCommnad!.ExecuteAsync(null);
                visited.Add(vm.CurrentStep?.Designation);
                marken.Add(vm.CurrentStep!);
            }

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            CollectionAssert.AreEqual(
                new[] { string.Empty, string.Empty, "Hinweis 1", "Hinweis 2", "Aufloesung" },
                visited.ToArray(),
                "Gesehen wurde: " + string.Join(" -> ", visited.Select(v => v ?? "null")) + ". " +
                "Die Abfolge stimmt nicht. Erwartet: leerer Startschritt (der Spielleiter liest "
                + "vor), dann der Fragebildschirm, dann die Hinweise, zuletzt die Aufloesung.");

            // Die beiden leeren sind NICHT derselbe Bildschirm - genau das war der Fehler,
            // der am 2026-09-06 gemeldet wurde.
            Assert.IsTrue(marken[0].IsStart && !marken[0].IsQuestionOnly,
                "Der erste Bildschirm ist nicht der Startschritt.");
            Assert.IsTrue(marken[1].IsQuestionOnly && !marken[1].IsStart,
                "Der zweite Bildschirm ist nicht der Fragebildschirm.");
            Assert.IsTrue(vm.CurrentStep?.IsFinish);
        }

        [TestMethod]
        public async Task SteppingForward_AsksBeforeRevealingTheAnswer()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();
            vm.Coordinate!.QuestionBase!.WarnOnResultStep = true;
            vm.Coordinate.QuestionBase.WarnOnFinishStep = true;

            await vm.StartStepCommnad!.ExecuteAsync(null);
            while (vm.NextStep != null)
                await vm.NextStepCommnad!.ExecuteAsync(null);

            CollectionAssert.Contains(prompt.Confirmations, "Lösungsschritt voraus");
        }

        [TestMethod]
        public async Task SteppingForward_WhenTheGameMasterSaysNo_StaysOnTheCurrentStep()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: false);

            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();
            vm.Coordinate!.QuestionBase!.WarnOnResultStep = true;

            // Start -> Fragebildschirm -> Hinweis. Erst der naechste Druck deckt auf.
            await vm.StartStepCommnad!.ExecuteAsync(null);
            await vm.NextStepCommnad!.ExecuteAsync(null);
            await vm.NextStepCommnad.ExecuteAsync(null);
            var before = vm.CurrentStep;

            Assert.IsFalse(before!.IsStart || before.IsQuestionOnly,
                "Es laeuft noch ein ergaenzter Bildschirm - dann steht die Aufloesung gar nicht "
                + "als naechstes an, und der Test misst die Rueckfrage nicht.");

            await vm.NextStepCommnad!.ExecuteAsync(null);

            Assert.AreSame(before, vm.CurrentStep,
                "Abgelehnt heisst stehenbleiben, nicht trotzdem aufdecken.");
        }

        [TestMethod]
        public async Task SteppingBack_ReturnsToThePreviousStep()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 2);
            var vm = OpenOn(world);
            await vm.LoadForTestAsync();

            // Start -> Startschritt, Fragebildschirm, Hinweis 1, Hinweis 2.
            await vm.StartStepCommnad!.ExecuteAsync(null);
            await vm.NextStepCommnad!.ExecuteAsync(null);
            await vm.NextStepCommnad.ExecuteAsync(null);
            await vm.NextStepCommnad.ExecuteAsync(null);
            Assert.AreEqual("Hinweis 2", vm.CurrentStep?.Designation);

            await vm.BackStepCommnad!.ExecuteAsync(null);

            Assert.AreEqual("Hinweis 1", vm.CurrentStep?.Designation);

            // Und zurueck bis auf den leeren Startschritt - der Rueckwaerts-Riegel vergleicht
            // gegen OrderedSteps.First(), und das ist seit 2026-09-06 ein ergaenztes Objekt.
            await vm.BackStepCommnad.ExecuteAsync(null);
            await vm.BackStepCommnad.ExecuteAsync(null);

            Assert.IsTrue(vm.CurrentStep?.IsStart,
                "Zurueck fuehrt nicht bis auf den Startschritt. Der Riegel vergleicht per "
                + "Verweis gegen ein Objekt, das CalculateOrderdSteps selbst erzeugt - ein "
                + "zweiter Lauf erzeugt ein anderes.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>
        /// Eine Frage ohne Hinweise: leerer Startschritt, dann direkt die Aufloesung.
        /// <para>
        /// <b>Dieser Fall ist keine Altlast.</b> Drei der zwoelf Demofragen sind so gebaut
        /// (Schaetzfragen: Start plus Aufloesung, nichts dazwischen). Wer die Anzeigeregel
        /// „erster Inhaltsschritt zeigt die Frage gross in der Mitte" ueber die blosse Zahl der
        /// vorangegangenen Schritte bestimmt, macht hier den <b>Abschlussschritt</b> zu diesem
        /// Bildschirm - und legt den Fragetext deckend ueber die Antwort.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AQuestionWithoutHints_StillGetsAnEmptyStartScreen()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 0);
            var vm = OpenOn(world);

            await vm.LoadForTestAsync();

            Assert.IsNotNull(vm.Coordinate?.QuestionBase);

            var schritte = vm.Coordinate.QuestionBase.OrderedSteps;

            Assert.AreEqual(3, schritte.Length,
                "Startschritt, Fragebildschirm, Aufloesung - genau drei.");
            Assert.IsTrue(schritte[0].IsStart, "Der erste Bildschirm ist kein Startschritt.");

            Assert.IsTrue(schritte[1].IsQuestionOnly,
                "Zwischen Start und Aufloesung fehlt der Fragebildschirm. Genau das war der "
                + "gemeldete Fehler: \"Schaetzfrage - Frage und Antwort zugleich\".");

            Assert.IsTrue(schritte[2].IsFinish, "Der dritte Bildschirm ist nicht die Aufloesung.");
        }

        [TestMethod]
        public async Task PlayingAMultipleChoiceQuestion_ShufflesButKeepsEveryOption()
        {
            await using var world = await TestGameBuilder.CreateAsync(
                questionType: QuestionType.MultipleChoice, normalStepCount: 4);
            var vm = OpenOn(world);

            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            // Der ergaenzte Fragebildschirm ist keine Antwortmoeglichkeit - er faellt hier
            // heraus, so wie er auch auf den Telefonen keine Taste bekommt.
            var options = vm.Coordinate!.QuestionBase!.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly)
                .Select(s => s.Designation)
                .OrderBy(d => d)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "Hinweis 1", "Hinweis 2", "Hinweis 3", "Hinweis 4" }, options);

            // Und die Tasten: genau vier, ohne Luecke am Anfang. Bekaeme der Fragebildschirm
            // eine, verschoeben sich alle Antworttasten auf den Telefonen um eine.
            var tasten = vm.Coordinate.QuestionBase.OrderedSteps
                .Where(s => !string.IsNullOrEmpty(s.QuestionViewKey))
                .Select(s => s.QuestionViewKey)
                .OrderBy(k => k)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, tasten,
                "Die Antworttasten stimmen nicht: " + string.Join(", ", tasten));
            Assert.AreEqual(BuzzerControlsLayout.KeySelect,
                vm.Coordinate.QuestionBase.BuzzerControlsLayout);
        }
    }
}
