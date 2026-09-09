using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.Views.QuestionTypes;
using Quizzer.Views.QuestionTypes.Typed;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// <b>Der Punkteverlauf in der Vorschau.</b>
    /// <para>
    /// <b>Nutzermeldung M1 vom 2026-09-09:</b> „ein punkt den du aufnehmen kannst ist dass in der
    /// vorschau auch der punkte lauf ... richtig bzw. falsch angezeigt wird". Die Vorschau zeigte
    /// die Bildschirme, ohne zu sagen, was auf ihnen zu holen ist - gerade dort sieht man die
    /// Frage aber so, wie sie am Beamer steht.
    /// </para>
    /// <para>
    /// <b>Die Zahlen stehen als Literale.</b> Sie aus <c>Punkteabzug</c> zu rechnen hiesse, die
    /// Anzeige gegen ihre eigene Quelle zu pruefen - dann bliebe sie gruen, egal was sie zeigt.
    /// </para>
    /// </summary>
    [TestClass]
    public class VorschauPunkteUnitTests
    {
        private static QuestionStepResource Schritt(string name, bool finish = false)
            => new() { Id = Guid.NewGuid(), Designation = name, StepText = name, IsFinish = finish };

        private static PropertiesQuestion Hinweisfrage()
        {
            var frage = new PropertiesQuestion { Id = Guid.NewGuid(), Points = 100, MinusPoints = 50 };

            frage.Steps.Add(Schritt("H1"));
            frage.Steps.Add(Schritt("H2"));
            frage.Steps.Add(Schritt("H3"));
            frage.Steps.Add(Schritt("Die Loesung", finish: true));

            return frage;
        }

        /// <summary>Läuft die Vorschau durch und schreibt je Bildschirm mit, was dort steht.</summary>
        private static List<string> Verlauf(QuestionBase frage)
        {
            var vm = new QuestionPreviewViewModel();

            vm.SetQuestion(frage);

            var zeilen = new List<string>();

            for (var i = 0; i < 20; i++)
            {
                zeilen.Add(vm.Punktestand);

                if (!vm.CanNext)
                    break;

                vm.NextCommand.Execute(null);
            }

            return zeilen;
        }

        /// <summary>
        /// Der gemessene Verlauf einer Hinweisfrage - Start und Fragebildschirm voll, dann die
        /// Kurve aus F16, und im Auflösungsschritt nichts mehr.
        /// </summary>
        [TestMethod]
        public void TheHintQuestionShowsItsCurve()
        {
            var verlauf = Verlauf(Hinweisfrage());

            CollectionAssert.AreEqual(
                new[]
                {
                    "richtig +100, falsch −50",
                    "richtig +100, falsch −50",
                    "richtig +67, falsch −50",
                    "richtig +34, falsch −50",
                    "richtig +17, falsch −50",
                    "richtig +0, falsch −50",
                },
                verlauf,
                "Der Verlauf stimmt nicht:\n" + string.Join("\n", verlauf));
        }

        /// <summary>
        /// <b>Die Minuspunkte sinken nicht mit</b> - und das ist keine Nachlässigkeit der
        /// Anzeige, sondern das Verhalten des Spiels: <c>PlayerResultContext</c> nimmt bei
        /// „falsch" immer <c>CurrentMinusPoints</c>, ohne Abzug je Hinweis.
        /// <para>
        /// <b>Ohne diese Zusicherung wäre die obige auch dann grün, wenn beide Werte an
        /// derselben Kurve hingen</b> - dann stünde in der Vorschau eine Zahl, die es im Spiel
        /// nicht gibt.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheMinusStaysTheSameOnEveryScreen()
        {
            var minuswerte = Verlauf(Hinweisfrage())
                .Select(z => z[(z.IndexOf("falsch", StringComparison.Ordinal))..])
                .Distinct()
                .ToArray();

            Assert.AreEqual(1, minuswerte.Length,
                "Der Minuswert aendert sich zwischen den Bildschirmen: "
                + string.Join(" / ", minuswerte));

            Assert.AreEqual("falsch −50", minuswerte[0]);
        }

        /// <summary>
        /// Ein Fragetyp <b>ohne</b> Punkteabzug je Schritt zeigt auf jedem Bildschirm denselben
        /// Pluswert - auch das ist das Verhalten des Spiels.
        /// </summary>
        [TestMethod]
        public void WithoutStepReductionThePlusStaysTheSame()
        {
            var frage = new DefaultQuestion { Id = Guid.NewGuid(), Points = 300, MinusPoints = 100 };

            frage.Steps.Add(Schritt("Ein Hinweis"));
            frage.Steps.Add(Schritt("Die Loesung", finish: true));

            Assert.IsFalse(frage.UseProportionalScoreReductionOnStep,
                "Die Standardfrage darf keinen Abzug je Schritt haben - sonst misst dieser Test "
                + "etwas anderes, als er behauptet.");

            var verlauf = Verlauf(frage).Distinct().ToArray();

            CollectionAssert.AreEqual(new[] { "richtig +300, falsch −100" }, verlauf,
                "Ohne Abzug je Schritt darf sich nichts aendern:\n" + string.Join("\n", verlauf));
        }

        /// <summary>
        /// <b>Vorschau und Frageneditor sagen dasselbe.</b>
        /// <para>
        /// Beides sind Zusagen an den Anlegenden, und sie stehen in verschiedenen Masken. Laufen
        /// sie auseinander, glaubt er der einen und bekommt die andere - genau der Fall, aus dem
        /// am 2026-09-07 der Abzug „einen Schritt hinterher" entstand.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ThePreviewAgreesWithTheEditorLadder()
        {
            var frage = Hinweisfrage();

            // Was der Editor neben die drei Hinweise schreibt.
            var leiter = new HinweisleiterViewModel(
                StepComposers.For(frage.Typ), frage, () => { });

            var ausEditor = leiter.Zeilen
                .Where(z => !z.IstLeer)
                .Select(z => z.Zusatz)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            Assert.AreEqual(3, ausEditor.Length,
                "Der Editor schreibt keine drei Zeilen - dann vergleicht dieser Test nichts: "
                + string.Join(" | ", ausEditor));

            // Was die Vorschau auf den drei Inhaltsschritten zeigt.
            var ausVorschau = Verlauf(frage)
                .Skip(2)
                .Take(3)
                .Select(z => z["richtig +".Length..z.IndexOf(',', StringComparison.Ordinal)])
                .ToArray();

            for (var i = 0; i < 3; i++)
            {
                StringAssert.Contains(ausEditor[i], ausVorschau[i],
                    $"Hinweis {i + 1}: der Editor sagt „{ausEditor[i]}\", die Vorschau zeigt "
                    + $"„{ausVorschau[i]}\".");
            }
        }
    }
}
