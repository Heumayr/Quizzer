using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Wo die Frage auf dem Beamer steht - über drei Bildschirme.
    /// <para>
    /// <b>Die Abfolge, entschieden vom Nutzer am 2026-09-06 (Fragen F02 und F05):</b>
    /// Bildschirm 1 nur die Fragenart, Bildschirm 2 der Fragetext groß in der Mitte, ab
    /// Bildschirm 3 der Fragetext schmal oben.
    /// </para>
    /// <para>
    /// <b>Bildschirm 1 trägt keinen Fragetext, und das ist die Spielregel.</b> Wörtlich:
    /// „der moderator liest die frage vor und bringt erst dann die frage zur anzeige ... buzzert
    /// ein spieler bevor die frage zu ende gestellt wurde, hat er nicht das recht die frage zu
    /// lesen". Bis zum Vormittag desselben Tages stand der Fragetext genau dort.
    /// </para>
    /// <para>
    /// <b>Die Vorrichtung legt bewusst KEINEN eigenen Startschritt an.</b> Sie tat es bis
    /// 2026-09-06, und dadurch war sie gegen die Startschritt-Automatik blind: gemessen hätte
    /// sie dasselbe, ob die Automatik da ist oder nicht.
    /// </para>
    /// </summary>
    [TestClass]
    public class QuestionPlacementUnitTests
    {
        private const string Fragetext = "Wie hoch ist der Großglockner?";

        private static QuestionStepResource Schritt(Guid frageId, int nummer, string text, bool istAbschluss = false)
            => new()
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frageId,
                SequenceNumber = nummer,
                StepText = text,
                Designation = text,
                IsFinish = istAbschluss,
            };

        private static DefaultQuestion Frage()
            => new()
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                DesignationShort = "P",
                QuestionText = Fragetext,
                Points = 100,
                Difficulty = Difficulty.Level1,
            };

        /// <summary>
        /// Eine Frage mit zwei Hinweisen und einer Auflösung - und <b>ohne</b> eigenen
        /// Startschritt. Nach <c>CalculateOrderdSteps</c> hat sie vier Bildschirme: der
        /// ergänzte Startschritt, zwei Inhaltsschritte, der Abschluss.
        /// </summary>
        private static (GamePlayerViewModel Vm, DefaultQuestion Frage) Baue()
        {
            var frage = Frage();

            frage.Steps.Add(Schritt(frage.Id, 10, "Er liegt in Österreich."));
            frage.Steps.Add(Schritt(frage.Id, 20, "Er ist der höchste Berg des Landes."));
            frage.Steps.Add(Schritt(frage.Id, 900, "3798 Meter", istAbschluss: true));

            frage.CalculateOrderdSteps();

            return (new GamePlayerViewModel(), frage);
        }

        /// <summary>
        /// Setzt genau die Größen, an denen die Anzeige hängt.
        /// <para>
        /// Bewusst nicht über einen <c>QuestionStepViewContext</c>: dessen <c>Question</c> kommt
        /// vom Besitzer und lässt sich nicht setzen. Der Setter von <c>QuestionStepResource</c>
        /// überschreibt außerdem den Fragetext - deshalb erst der Schritt, dann der Rest.
        /// </para>
        /// </summary>
        private static void Zeige(GamePlayerViewModel vm, QuestionBase frage, int bildschirm)
        {
            // Reihenfolge: erst der Schritt, dann alles andere. Der Setter von
            // QuestionStepResource holt sich Schrittliste, Fragenart und Fragetext aus dem
            // QuestionStepViewContext - und der ist hier null, also raeumt er sie aus. Wer
            // vorher setzt, misst leere Werte.
            vm.QuestionStepResource = frage.OrderedSteps[bildschirm];
            vm.OrderedSteps = frage.OrderedSteps;
            vm.QuestionText = frage.QuestionText;
            vm.QuestionTypeName = frage.TypDisplayName;
        }

        /// <summary>
        /// Bildschirm 1: nur die Fragenart. Der Fragetext steht nirgends - weder oben noch
        /// mittig.
        /// </summary>
        [TestMethod]
        public void OnTheStartScreenOnlyTheQuestionTypeIsShown()
        {
            var (vm, frage) = Baue();

            Zeige(vm, frage, bildschirm: 0);

            Assert.IsTrue(vm.IsStartStep, "Der Startschritt wurde nicht als solcher erkannt.");

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionTypeCentered,
                "Die Fragenart steht nicht da. Dann sehen die Mitspieler einen leeren Bildschirm "
                + "und wissen nicht, worauf sie sich einstellen.");

            Assert.AreEqual("Standardfrage", vm.QuestionTypeName,
                "Der Anzeigename kommt nicht aus den Fragetyp-Profilen.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered,
                "Der Fragetext steht gross in der Mitte. Wer zu frueh buzzert, liest ihn mit - "
                + "genau das verbietet die Spielregel.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTop,
                "Der Fragetext steht oben. Auch dort darf er auf dem Startbildschirm nicht sein.");
        }

        /// <summary>Bildschirm 2: der Fragetext groß in der Mitte, die Fragenart weg.</summary>
        [TestMethod]
        public void OnTheFirstContentScreenTheQuestionIsInTheMiddle()
        {
            var (vm, frage) = Baue();

            Zeige(vm, frage, bildschirm: 1);

            Assert.IsTrue(vm.IsFirstContentStep,
                "Der erste Inhaltsschritt wurde nicht als solcher erkannt.");

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionCentered,
                "Die Frage steht nicht gross in der Mitte - das ist der Augenblick der Freigabe.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTop,
                "Die Frage steht zusaetzlich oben - genau die Doppelung, die gemeldet wurde.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTypeCentered,
                "Die Fragenart steht noch da und verdeckt die Frage.");
        }

        /// <summary>Ab Bildschirm 3 wandert die Frage nach oben und macht der Sache Platz.</summary>
        [TestMethod]
        public void FromTheThirdScreenOnTheQuestionMovesToTheTop()
        {
            var (vm, frage) = Baue();

            Zeige(vm, frage, bildschirm: 2);

            Assert.IsFalse(vm.IsStartStep);
            Assert.IsFalse(vm.IsFirstContentStep);

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionTop,
                "Ab dem dritten Bildschirm gehoert die Frage nach oben.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered,
                "Die grosse Anzeige in der Mitte bleibt stehen und verdeckt den Schritt.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTypeCentered);
        }

        /// <summary>
        /// Der Wechsel wirkt wirklich - über alle vier Bildschirme an derselben Instanz.
        /// <para>
        /// Ohne diese Probe wären die Zusicherungen oben auch dann grün, wenn der Zustand nach
        /// dem ersten Schritt einfriert: jede setzt für sich genau einen Schritt.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheQuestionActuallyMovesWhenSteppingForward()
        {
            var (vm, frage) = Baue();

            Assert.AreEqual(4, frage.OrderedSteps.Length,
                "Die Vorrichtung hat nicht vier Bildschirme. Dann misst dieser Durchlauf etwas "
                + "anderes als den vollen Weg.");

            var verlauf = new List<string>();

            for (var i = 0; i < frage.OrderedSteps.Length; i++)
            {
                Zeige(vm, frage, bildschirm: i);

                verlauf.Add(
                    vm.ShowQuestionTypeCentered == Visibility.Visible ? "Art"
                    : vm.ShowQuestionCentered == Visibility.Visible ? "Mitte"
                    : vm.ShowQuestionTop == Visibility.Visible ? "oben"
                    : "nichts");
            }

            CollectionAssert.AreEqual(
                new[] { "Art", "Mitte", "oben", "oben" }, verlauf.ToArray(),
                "Der Verlauf ueber die vier Bildschirme stimmt nicht: "
                + string.Join(" -> ", verlauf));
        }

        /// <summary>
        /// Eine Frage ohne Inhaltsschritt: der Fragetext steht auf dem zweiten Bildschirm
        /// <b>oben</b>, nicht mittig.
        /// <para>
        /// <b>Drei der zwölf Demofragen sind so gebaut</b> - Schätzfragen mit Startschritt und
        /// Auflösung, nichts dazwischen. Bestimmte man den ersten Inhaltsschritt über die Zahl
        /// der vorangegangenen Schritte, träfe es hier den <b>Abschluss</b>: der Fragetext läge
        /// groß und deckend über der Antwort, und die Mitspieler sähen sie nie.
        /// </para>
        /// </summary>
        [TestMethod]
        public void WithoutAContentStepTheAnswerIsNotCoveredByTheQuestion()
        {
            var frage = Frage();

            frage.Steps.Add(Schritt(frage.Id, 900, "3798 Meter", istAbschluss: true));
            frage.CalculateOrderdSteps();

            var vm = new GamePlayerViewModel();

            Assert.AreEqual(2, frage.OrderedSteps.Length,
                "Ergaenzter Startschritt und Abschluss - mehr hat diese Frage nicht.");

            Zeige(vm, frage, bildschirm: 1);

            Assert.IsTrue(vm.QuestionStepResource!.IsFinish,
                "Der zweite Bildschirm ist nicht der Abschluss - die Vorrichtung misst den "
                + "gefaehrlichen Fall gar nicht.");

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered,
                "Der Fragetext liegt gross und deckend ueber der Antwort.");

            Assert.AreEqual(Visibility.Visible, vm.ShowQuestionTop,
                "Der Fragetext steht nirgends. Die Mitspieler sehen eine Antwort ohne Frage.");
        }

        /// <summary>Ohne Fragetext bleibt beides weg - sonst stünde ein leerer Kasten da.</summary>
        [TestMethod]
        public void WithoutAQuestionTextNeitherIsShown()
        {
            var (vm, frage) = Baue();

            frage.QuestionText = string.Empty;

            Zeige(vm, frage, bildschirm: 1);

            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionCentered);
            Assert.AreEqual(Visibility.Collapsed, vm.ShowQuestionTop);
        }
    }
}
