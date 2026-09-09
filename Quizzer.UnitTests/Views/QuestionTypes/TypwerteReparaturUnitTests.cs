using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using Quizzer.Views.QuestionTypes;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// <b>Eine Frage, die von ihrem Fragetyp abweicht, muss sich reparieren lassen.</b>
    /// <para>
    /// <b>Gemessen am 2026-09-09 an der echten Spieldatenbank:</b> die Frage „Appre Frage" trug
    /// <c>QuestionViewKeyType = Alphabetical</c>, das Profil der Schätzfrage verlangt
    /// <c>Numerical</c>. Drei Dinge trafen zusammen und ergaben eine Sackgasse: der Prüfer meldet
    /// es als <b>Fehler</b>, <c>CanSave</c> sperrt daraufhin das Speichern, und der Wert ist in
    /// der Maske nirgends änderbar, weil ihn der Typ setzt. Es blieb nur, die Frage zu löschen.
    /// </para>
    /// </summary>
    [TestClass]
    public class TypwerteReparaturUnitTests
    {
        /// <summary>Genau der Zustand aus der Spieldatenbank, nachgebaut.</summary>
        private static EditQuestionViewModel MitAbweichung()
            => ZeileneditorUnitTests.Mit(QuestionType.Appreciate, f =>
            {
                f.DesignationShort = "AF";
                f.CategoryId = Guid.NewGuid();
                f.QuestionViewKeyType = QuestionViewKeyType.Alphabetical;
            });

        /// <summary>
        /// Die Sackgasse selbst - sie wird hier festgehalten, damit niemand den Knopf für
        /// überflüssig hält und wieder ausbaut.
        /// </summary>
        [TestMethod]
        public void ADriftedQuestionIsBlockedFromSaving()
        {
            var vm = MitAbweichung();

            Assert.IsTrue(vm.HasTypeOwnedDrift,
                "Die Abweichung wird nicht erkannt - dann sagt der Rest dieser Klasse nichts.");

            CollectionAssert.Contains(
                vm.Issues.Where(i => i.IsError).Select(i => i.Code).ToArray(),
                QuestionValidator.TypeOwnedValuesChanged,
                "Die Abweichung muss ein Fehler sein, kein Hinweis.");

            Assert.IsFalse(vm.CanSave,
                "Wenn sich die abweichende Frage speichern liesse, gaebe es die Sackgasse nicht "
                + "- und dieser Test misst dann etwas anderes als er behauptet.");
        }

        /// <summary>Und der Knopf führt heraus.</summary>
        [TestMethod]
        public void TheRepairButtonGetsTheQuestionBackToSavable()
        {
            var vm = MitAbweichung();

            Assert.IsTrue(vm.RepairTypeOwnedValuesCommand.CanExecute(null),
                "Der Knopf muss bei einer abweichenden Frage bedienbar sein.");

            vm.RepairTypeOwnedValuesCommand.Execute(null);

            Assert.IsFalse(vm.HasTypeOwnedDrift, "Die Abweichung besteht weiter.");

            Assert.AreEqual(QuestionViewKeyType.Numerical, vm.Question!.QuestionViewKeyType,
                "Der abweichende Wert steht nicht auf der Vorgabe des Profils.");

            Assert.IsTrue(vm.CanSave,
                "Nach der Reparatur muss sich die Frage speichern lassen. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));
        }

        /// <summary>
        /// <b>Die Reparatur fasst nur die typeigenen Werte an.</b> Sie darf nicht nebenbei Text,
        /// Punkte oder Schritte anrühren - sonst wäre der Knopf gefährlicher als die Sackgasse.
        /// </summary>
        [TestMethod]
        public void TheRepairTouchesNothingElse()
        {
            var vm = MitAbweichung();
            var frage = vm.Question!;

            frage.QuestionText = "Wie hoch ist der Grossglockner?";
            frage.Points = 300;
            frage.MinusPoints = 100;
            frage.Notes = "Bleibt stehen";

            vm.RepairTypeOwnedValuesCommand.Execute(null);

            Assert.AreEqual("Wie hoch ist der Grossglockner?", frage.QuestionText);
            Assert.AreEqual(300, frage.Points);
            Assert.AreEqual(100, frage.MinusPoints);
            Assert.AreEqual("Bleibt stehen", frage.Notes);
            Assert.AreEqual("Probe", frage.Designation);
        }

        /// <summary>
        /// Ohne Abweichung ist der Knopf nicht bedienbar - er soll nicht zum Werkzeug werden,
        /// mit dem man eine Frage „mal eben zuruecksetzt".
        /// </summary>
        [TestMethod]
        public void WithoutDriftTheButtonStaysDisabled()
        {
            var vm = ZeileneditorUnitTests.Mit(QuestionType.Appreciate, f =>
            {
                f.DesignationShort = "AF";
                f.CategoryId = Guid.NewGuid();
            });

            Assert.IsFalse(vm.HasTypeOwnedDrift,
                "Eine frisch angelegte Frage darf nicht abweichen - der Konstruktor setzt das "
                + "Profil selbst.");

            Assert.IsFalse(vm.RepairTypeOwnedValuesCommand.CanExecute(null));
        }

        /// <summary>
        /// <b>Die Gegenprobe in die andere Richtung:</b> der Knopf richtet <i>jeden</i> der fünf
        /// typeigenen Werte, nicht nur den einen, an dem er gemessen wurde.
        /// <para>
        /// Ohne sie bliebe die Klasse grün, wenn die Reparatur nur <c>QuestionViewKeyType</c>
        /// setzte - und die nächste Abweichung wäre wieder eine Sackgasse.
        /// </para>
        /// </summary>
        [TestMethod]
        public void EveryTypeOwnedValueIsRepaired()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var profil = QuestionTypeProfiles.For(typ);

                var vm = ZeileneditorUnitTests.Mit(typ, f =>
                {
                    f.DesignationShort = "X";
                    f.CategoryId = Guid.NewGuid();

                    // Jeden der fuenf Werte verstellen - je auf das Gegenteil der Vorgabe.
                    f.QuestionViewKeyType = profil.QuestionViewKeyType == QuestionViewKeyType.Numerical
                        ? QuestionViewKeyType.Alphabetical
                        : QuestionViewKeyType.Numerical;
                    f.StepDisplayLayoutMode = profil.StepDisplayLayoutMode == StepDisplayLayoutMode.Vertical
                        ? StepDisplayLayoutMode.Horizontal
                        : StepDisplayLayoutMode.Vertical;
                    f.BuzzerControlsLayout = profil.BuzzerControlsLayout == BuzzerControlsLayout.Buzzer
                        ? BuzzerControlsLayout.Input
                        : BuzzerControlsLayout.Buzzer;
                    f.UseRandomSequenceOnNoneFinishSteps = !profil.UseRandomSequenceOnNoneFinishSteps;
                    f.UseProportionalScoreReductionOnStep = !profil.UseProportionalScoreReductionOnStep;
                });

                Assert.IsTrue(vm.HasTypeOwnedDrift, $"{typ}: die Abweichung wird nicht erkannt.");

                vm.RepairTypeOwnedValuesCommand.Execute(null);

                Assert.IsFalse(vm.HasTypeOwnedDrift,
                    $"{typ}: nach der Reparatur weicht die Frage immer noch ab.");
            }
        }
    }
}
