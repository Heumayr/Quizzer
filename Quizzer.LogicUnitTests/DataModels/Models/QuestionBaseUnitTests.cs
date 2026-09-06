using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.LogicUnitTests.DataModels.Models
{
    /// <summary>
    /// Reihenfolge und Navigation der Frageschritte - der Kern des Durchspielens.
    /// </summary>
    [TestClass]
    public class QuestionBaseUnitTests
    {
        [TestCleanup]
        public void RestoreRandomizer() => QuestionBase.Randomizer = Random.Shared;

        private static QuestionStepResource Step(string designation, int sequenceNumber,
            bool isResult = false, bool isFinish = false)
            => new()
            {
                Id = Guid.NewGuid(),
                Designation = designation,
                SequenceNumber = sequenceNumber,
                IsResult = isResult,
                IsFinish = isFinish,
            };

        private static DefaultQuestion QuestionWith(params QuestionStepResource[] steps)
        {
            var question = new DefaultQuestion();
            question.Steps.AddRange(steps);
            return question;
        }

        [TestMethod]
        public void CalculateOrderdSteps_WithoutSteps_YieldsNothing()
        {
            var question = QuestionWith();

            question.CalculateOrderdSteps();

            Assert.AreEqual(0, question.OrderedSteps.Length);
        }

        [TestMethod]
        public void CalculateOrderdSteps_SortsNormalStepsBySequenceNumber()
        {
            var question = QuestionWith(
                Step("dritter", 30), Step("erster", 10), Step("zweiter", 20));

            question.CalculateOrderdSteps();

            var normal = question.OrderedSteps.Where(s => !s.IsStart && !s.IsFinish).ToList();
            CollectionAssert.AreEqual(
                new[] { "erster", "zweiter", "dritter" },
                normal.Select(s => s.Designation).ToArray());
        }

        /// <summary>
        /// Die Nummern laufen in Zehnerschritten bei 0 los - unabhaengig davon, was vorher
        /// dranstand.
        /// <para>
        /// <b>Bewusst keine abgeschriebene Zahlenreihe.</b> Hier stand bis 2026-09-06
        /// <c>{ 0, 10, 20 }</c>; als die Startschritt-Automatik zurueckkam, verschob sich jede
        /// Nummer um zehn, und die Zusicherung fiel. Eine nackte Reihe haette man dann einfach
        /// nachgezogen und die Verschiebung mit zugedeckt. Gemessen wird deshalb der
        /// <b>Schritt</b> und die <b>Laenge relativ zum Bestand</b>.
        /// </para>
        /// </summary>
        [TestMethod]
        public void CalculateOrderdSteps_RenumbersInStepsOfTen()
        {
            var question = QuestionWith(Step("a", 7), Step("b", 999));

            question.CalculateOrderdSteps();

            var nummern = question.OrderedSteps.Select(s => s.SequenceNumber).ToArray();

            // Zwei eigene Schritte plus je ein ergaenzter Start- und Abschlussschritt.
            Assert.AreEqual(question.Steps.Count + 2, nummern.Length,
                "Es wurde nicht genau ein Start- und ein Abschlussschritt ergaenzt.");

            Assert.AreEqual(0, nummern[0], "Die Zaehlung beginnt nicht bei 0.");

            for (var i = 1; i < nummern.Length; i++)
            {
                Assert.AreEqual(10, nummern[i] - nummern[i - 1],
                    $"Zwischen Schritt {i - 1} und {i} liegen nicht zehn: "
                    + string.Join(", ", nummern));
            }
        }

        [TestMethod]
        public void CalculateOrderdSteps_AppendsFinishStepWhenNoneAuthored()
        {
            var question = QuestionWith(Step("a", 10));

            question.CalculateOrderdSteps();

            Assert.IsTrue(question.OrderedSteps.Last().IsFinish);
        }

        [TestMethod]
        public void CalculateOrderdSteps_KeepsAuthoredFinishStepLast()
        {
            var question = QuestionWith(
                Step("aufloesung", 99, isResult: true, isFinish: true), Step("hinweis", 10));

            question.CalculateOrderdSteps();

            Assert.AreEqual("aufloesung", question.OrderedSteps.Last().Designation);
        }

        /// <summary>
        /// Ohne hinterlegten Startschritt wird einer ergaenzt - und er bleibt draussen.
        /// <para>
        /// <b>Die Geschichte in zwei Saetzen.</b> Bis zum 21.08.2026 wurde hier immer einer
        /// erfunden; das wurde ausgebaut, weil der leere erste Bildschirm fuer einen Mangel
        /// gehalten wurde. Am 2026-09-06 hat der Nutzer widersprochen: der leere Bildschirm ist
        /// die <b>Spielregel</b> - der Spielleiter liest vor, und wer zu frueh buzzert, darf
        /// nicht mitlesen. Diese Zusicherung stand deshalb bis dahin auf dem Kopf.
        /// </para>
        /// <para>
        /// <b>Dass der Schritt nicht in <c>Steps</c> landet, ist kein Beiwerk.</b> Laege er
        /// dort, schriebe ihn der naechste Speichervorgang in die Datenbank, und der
        /// Fragepruefer meldete beim zweiten Laden <c>MultipleStartSteps</c> und sperrte das
        /// Speichern.
        /// </para>
        /// </summary>
        [TestMethod]
        public void CalculateOrderdSteps_WithoutAnAuthoredStartStep_InventsAnEmptyOne()
        {
            var question = QuestionWith(Step("hinweis", 10));

            question.CalculateOrderdSteps();

            var first = question.OrderedSteps.First();

            Assert.IsTrue(first.IsStart,
                "Der erste Bildschirm ist kein Startschritt. Dann steht der Fragetext sofort da, "
                + "und wer zu frueh buzzert, liest mit.");

            Assert.AreEqual(string.Empty, first.StepText,
                "Der ergaenzte Startschritt traegt Text. Er soll leer sein - der Spielleiter "
                + "liest vor.");

            Assert.IsFalse(question.Steps.Contains(first),
                "Der ergaenzte Schritt liegt in Steps. Dann wird er mitgespeichert, und beim "
                + "zweiten Laden meldet der Fragepruefer MultipleStartSteps.");

            Assert.AreEqual(Guid.Empty, first.QuestionBaseId,
                "Der ergaenzte Schritt traegt eine Fragezuordnung - dann sieht er aus wie ein "
                + "echter und wird irgendwann als einer behandelt.");

            // Die Gegenrichtung: ein hinterlegter Startschritt wird nicht verdoppelt.
            var mitEigenem = QuestionWith(Step("hinweis", 10));
            var intro = Step("Intro", 5);
            intro.IsStart = true;
            mitEigenem.Steps.Add(intro);

            mitEigenem.CalculateOrderdSteps();

            Assert.AreEqual(1, mitEigenem.OrderedSteps.Count(s => s.IsStart),
                "Neben dem hinterlegten Startschritt wurde noch einer ergaenzt.");
        }

        [TestMethod]
        public void CalculateOrderdSteps_PutsAnAuthoredStartStepFirst()
        {
            var question = QuestionWith(Step("hinweis", 10));
            var intro = Step("Intro", 5);
            intro.IsStart = true;
            question.Steps.Add(intro);

            question.CalculateOrderdSteps();

            Assert.AreEqual("Intro", question.OrderedSteps.First().Designation);
            Assert.IsTrue(question.OrderedSteps.First().IsStart);
        }

        [TestMethod]
        public void CalculateOrderdSteps_GivesTheStartStepNoAnswerKey()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20));
            var intro = Step("Intro", 5);
            intro.IsStart = true;
            question.Steps.Add(intro);

            question.CalculateOrderdSteps();

            Assert.AreEqual(string.Empty, question.OrderedSteps.First().QuestionViewKey);
            var keys = question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.QuestionViewKey).ToArray();
            CollectionAssert.AreEqual(new[] { "A", "B" }, keys);
        }

        [TestMethod]
        public void CalculateOrderdSteps_AssignsViewKeysOnlyToNormalSteps()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20), Step("c", 30));

            question.CalculateOrderdSteps();

            var keys = question.OrderedSteps.Select(s => s.QuestionViewKey).ToArray();

            // Vorn der ergaenzte Startschritt, hinten der ergaenzte Abschluss - beide ohne Taste.
            CollectionAssert.AreEqual(new[] { string.Empty, "A", "B", "C", string.Empty }, keys,
                "Start- und Abschlussschritt bleiben ohne Antworttaste, die drei normalen nicht.");

            Assert.AreEqual(
                question.OrderedSteps.Count(s => !s.IsStart && !s.IsFinish),
                keys.Count(k => !string.IsNullOrEmpty(k)),
                "Es tragen nicht genau die normalen Schritte eine Taste.");
        }

        [TestMethod]
        public void CalculateOrderdSteps_UsesNumericKeysWhenConfigured()
        {
            var question = new PropertiesQuestion();
            question.Steps.AddRange([Step("a", 10), Step("b", 20)]);

            question.CalculateOrderdSteps();

            var keys = question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.QuestionViewKey).ToArray();
            CollectionAssert.AreEqual(new[] { "1", "2" }, keys);
        }

        [TestMethod]
        public void CalculateOrderdSteps_WhenShuffling_KeepsEveryNormalStepExactlyOnce()
        {
            var question = new MultipleChoiceQuestion();
            question.Steps.AddRange([Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)]);
            Assert.IsTrue(question.UseRandomSequenceOnNoneFinishSteps);

            question.CalculateOrderdSteps();

            var names = question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation)
                .OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, names);
        }

        [TestMethod]
        public void CalculateOrderdSteps_WhenShuffling_SameSeedGivesSameOrder()
        {
            QuestionBase.Randomizer = new Random(4711);
            var question = new MultipleChoiceQuestion();
            question.Steps.AddRange([Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)]);
            question.CalculateOrderdSteps();
            var first = question.OrderedSteps.Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation).ToArray();

            QuestionBase.Randomizer = new Random(4711);
            var again = new MultipleChoiceQuestion();
            again.Steps.AddRange([Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)]);
            again.CalculateOrderdSteps();
            var second = again.OrderedSteps.Where(s => !s.IsStart && !s.IsFinish)
                .Select(s => s.Designation).ToArray();

            CollectionAssert.AreEqual(first, second);
        }

        [TestMethod]
        public void GetNextStep_WalksForwardAndStopsAtTheEnd()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20));
            question.CalculateOrderdSteps();

            var current = question.OrderedSteps.First();
            var visited = 1;

            while (question.GetNextStep(current) is { } next)
            {
                current = next;
                visited++;
            }

            Assert.AreEqual(question.OrderedSteps.Length, visited);
            Assert.IsTrue(current.IsFinish);
        }

        [TestMethod]
        public void GetStepBehind_WalksBackOneStep()
        {
            var question = QuestionWith(Step("a", 10), Step("b", 20));
            question.CalculateOrderdSteps();

            var back = question.GetStepBehind(question.OrderedSteps.Last());

            Assert.AreEqual("b", back?.Designation);
        }

        [TestMethod]
        public void GetStepBehind_FromFirstStep_YieldsNothing()
        {
            var question = QuestionWith(Step("a", 10));
            question.CalculateOrderdSteps();

            Assert.IsNull(question.GetStepBehind(question.OrderedSteps.First()));
        }
    }
}
