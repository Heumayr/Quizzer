using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.QuestionTypes;
using System.Windows;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Vorschau im Frageneditor - dieselbe Abfolge wie am Abend, und ohne jede Nebenwirkung.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „ein vorschaumodus im frageneditor ... dass man direkt
    /// dort die fragen ausprobieren kann".
    /// </para>
    /// <para>
    /// <b>Die wichtigere der beiden Zusicherungen ist die zweite.</b> Die Vorschau ordnet die
    /// Schritte, und dabei ergänzt das Modell Bildschirme. Arbeitete sie auf der Frage selbst,
    /// stünden diese ergänzten Bildschirme danach in der Schrittliste des Editors - und der
    /// nächste Speichervorgang schriebe sie in die Datenbank.
    /// </para>
    /// </summary>
    [TestClass]
    public class QuestionPreviewUnitTests
    {
        private static QuestionBase Frage()
        {
            var frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                QuestionText = "Wie hoch ist der Großglockner?",
                Points = 100,
                Difficulty = Difficulty.Level1,
            };

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(), SequenceNumber = 10, StepText = "Er liegt in Österreich.",
            });

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(), SequenceNumber = 900, StepText = "3798 Meter", IsFinish = true,
            });

            return frage;
        }

        /// <summary>
        /// Die Vorschau zeigt dieselbe Abfolge wie der Beamer: Fragenart, Frage, Inhalt,
        /// Abschluss.
        /// </summary>
        [TestMethod]
        public void ThePreviewWalksTheSameSequenceAsTheBeamer()
        {
            var vm = new QuestionPreviewViewModel();

            vm.SetQuestion(Frage());

            var verlauf = new List<string>();

            do
            {
                verlauf.Add(
                    vm.Beamer.ShowQuestionTypeCentered == Visibility.Visible ? "Art"
                    : vm.Beamer.ShowQuestionCentered == Visibility.Visible ? "Mitte"
                    : vm.Beamer.ShowQuestionTop == Visibility.Visible ? "oben"
                    : "nichts");

                if (!vm.CanNext)
                    break;

                vm.NextCommand.Execute(null);
            }
            while (true);

            CollectionAssert.AreEqual(
                new[] { "Art", "Mitte", "oben", "oben" }, verlauf.ToArray(),
                "Die Vorschau zeigt eine andere Abfolge als der Beamer: "
                + string.Join(" -> ", verlauf));

            Assert.IsFalse(vm.CanNext, "Nach dem letzten Bildschirm geht es weiter.");
            Assert.IsTrue(vm.CanBack, "Zurueck ist gesperrt, obwohl man mittendrin steht.");
        }

        /// <summary>
        /// <b>Die Vorschau lässt die Frage im Editor unberührt.</b> Sie ordnet die Schritte, und
        /// dabei ergänzt das Modell welche - die dürfen nicht in der Liste des Editors landen.
        /// </summary>
        [TestMethod]
        public void ThePreviewDoesNotTouchTheEditedQuestion()
        {
            var frage = Frage();

            var schritteVorher = frage.Steps.Count;
            var ids = frage.Steps.Select(s => s.Id).ToList();

            var vm = new QuestionPreviewViewModel();

            vm.SetQuestion(frage);
            vm.NextCommand.Execute(null);
            vm.NextCommand.Execute(null);

            Assert.AreEqual(schritteVorher, frage.Steps.Count,
                "Die Vorschau hat der Frage Schritte hinzugefuegt. Der naechste Speichervorgang "
                + "schriebe sie in die Datenbank.");

            CollectionAssert.AreEqual(ids, frage.Steps.Select(s => s.Id).ToList(),
                "Die Schritte der Frage wurden ausgetauscht.");

            Assert.AreEqual(0, frage.OrderedSteps.Length,
                "Die Vorschau hat OrderedSteps der Originalfrage gefuellt - dann arbeitet sie "
                + "nicht auf einem Klon.");

            // Und die Vorschau selbst hat sehr wohl die ergaenzten Bildschirme.
            // Zwei eigene Schritte plus Startbildschirm und Fragebildschirm.
            Assert.AreEqual(schritteVorher + 2, vm.Question!.OrderedSteps.Length,
                "Die Vorschau zeigt nicht die ergaenzten Bildschirme - dann misst die "
                + "Zusicherung oben etwas anderes als das Spiel.");
        }

        /// <summary>Eine Frage ohne Schritte sagt es, statt leer dazustehen.</summary>
        [TestMethod]
        public void AQuestionWithoutStepsSaysSo()
        {
            var leer = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Leer" };

            var vm = new QuestionPreviewViewModel();

            vm.SetQuestion(leer);

            Assert.IsTrue(vm.Standanzeige.Contains("keine Schritte", StringComparison.OrdinalIgnoreCase),
                "Der Hinweis fehlt. Gelesen wurde: " + vm.Standanzeige);

            Assert.IsFalse(vm.CanNext);
            Assert.IsFalse(vm.CanBack);
        }
    }
}
