using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Wo die Frage auf dem Beamer steht.
    /// <para>
    /// <b>Gemeldet 2026-09-06:</b> „beim ersten step ... steht die frage eigentlich immer oben
    /// ... sie ein zweites mal mittig anzuzeigen ist eher unschön ... ggf. zuerst im ersten step
    /// groß in der mitte ... dann oben". Genau so ist es jetzt.
    /// </para>
    /// </summary>
    [TestClass]
    public class QuestionPlacementUnitTests
    {
        private static QuestionStepResource Schritt(Guid frageId, int nummer, string text, bool istStart)
            => new()
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frageId,
                SequenceNumber = nummer,
                StepText = text,
                Designation = text,
                IsStart = istStart,
            };

        private static (GamePlayerViewModel Vm, QuestionBase Frage) Baue()
        {
            var frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                DesignationShort = "P",
                QuestionText = "Wie hoch ist der Großglockner?",
                Points = 100,
                Difficulty = Difficulty.Level1,
            };

            frage.Steps.Add(Schritt(frage.Id, 1, string.Empty, istStart: true));
            frage.Steps.Add(Schritt(frage.Id, 10, "Er liegt in Österreich.", istStart: false));

            frage.CalculateOrderdSteps();

            return (new GamePlayerViewModel(), frage);
        }

        /// <summary>
        /// Setzt genau die beiden Groessen, an denen die Anzeige haengt: den laufenden Schritt
        /// und den Fragetext.
        /// <para>
        /// Bewusst nicht ueber einen <c>QuestionStepViewContext</c>: dessen <c>Question</c> kommt
        /// vom Besitzer und laesst sich nicht setzen. Der Setter von
        /// <c>QuestionStepResource</c> ueberschreibt ausserdem den Fragetext - deshalb erst der
        /// Schritt, dann der Text.
        /// </para>
        /// </summary>
        private static void Zeige(GamePlayerViewModel vm, QuestionBase frage, bool startschritt)
        {
            vm.QuestionStepResource = frage.OrderedSteps.First(s => s.IsStart == startschritt);
            vm.QuestionText = frage.QuestionText;
        }

        /// <summary>Im ersten Schritt steht die Frage groß in der Mitte, nicht oben.</summary>
        [TestMethod]
        public void OnTheFirstStepTheQuestionIsInTheMiddle()
        {
            var (vm, frage) = Baue();

            Zeige(vm, frage, startschritt: true);

            Assert.IsTrue(vm.IsStartStep, "Der Startschritt wurde nicht als solcher erkannt.");

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionCentered,
                "Die Frage steht im ersten Schritt nicht gross in der Mitte.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTop,
                "Die Frage steht zusaetzlich oben - genau die Doppelung, die gemeldet wurde.");
        }

        /// <summary>
        /// Ab dem zweiten Schritt wandert sie nach oben und macht der Sache Platz. Die
        /// Gegenrichtung: ohne sie waere die Probe oben auch dann gruen, wenn die Kopfzeile nie
        /// erschiene.
        /// </summary>
        [TestMethod]
        public void FromTheSecondStepOnTheQuestionMovesToTheTop()
        {
            var (vm, frage) = Baue();

            Zeige(vm, frage, startschritt: false);

            Assert.IsFalse(vm.IsStartStep);

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionTop,
                "Ab dem zweiten Schritt gehoert die Frage nach oben.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered,
                "Die grosse Anzeige in der Mitte bleibt stehen und verdeckt den Schritt.");
        }

        /// <summary>
        /// Der Wechsel wirkt wirklich - erst Mitte, dann oben, an derselben Instanz. Ohne diese
        /// Probe waeren die beiden oben auch dann gruen, wenn der Zustand nach dem ersten
        /// Schritt einfriert.
        /// </summary>
        [TestMethod]
        public void TheQuestionActuallyMovesWhenSteppingForward()
        {
            var (vm, frage) = Baue();

            Zeige(vm, frage, startschritt: true);

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionCentered);

            Zeige(vm, frage, startschritt: false);

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered,
                "Nach dem Weiterschalten steht die Frage immer noch gross in der Mitte.");

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionTop,
                "Nach dem Weiterschalten kam die Kopfzeile nicht.");
        }

        /// <summary>Ohne Fragetext bleibt beides weg - sonst stuende ein leerer Kasten da.</summary>
        [TestMethod]
        public void WithoutAQuestionTextNeitherIsShown()
        {
            var (vm, frage) = Baue();

            frage.QuestionText = string.Empty;

            Zeige(vm, frage, startschritt: true);

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered);
            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTop);
        }
    }
}
