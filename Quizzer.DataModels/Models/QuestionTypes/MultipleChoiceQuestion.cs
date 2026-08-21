using Quizzer.DataModels.Enumerations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Multiple-Choice-Frage: Spieler wählen im Browser eine oder mehrere Antwort-Optionen
    /// über das KeySelect-Layout aus. Die Reihenfolge der Optionen wird zufällig gemischt
    /// und im Grid-Layout angezeigt. Gespeichert in <c>question.MultipleChoiceQuestion</c>.
    /// </summary>
    [Table(nameof(MultipleChoiceQuestion), Schema = "question")]
    public sealed class MultipleChoiceQuestion : QuestionBase
    {
        /// <summary>
        /// Initialisiert eine neue Multiple-Choice-Frage mit Standardwerten:
        /// 100 Punkte, KeySelect-Buzzer-Layout, zufällige Schrittfolge, Grid-Anzeigemodus.
        /// </summary>
        public MultipleChoiceQuestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.MultipleChoice;
            WarnOnResultStep = false;
            UseRandomSequenceOnNoneFinishSteps = true;
            DefaultFinishType = FinishType.AllPreviousSteps;
            BuzzerControlsLayout = BuzzerControlsLayout.KeySelect;
            StepDisplayLayoutMode = StepDisplayLayoutMode.Grid;
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new MultipleChoiceQuestion();
        }
    }
}