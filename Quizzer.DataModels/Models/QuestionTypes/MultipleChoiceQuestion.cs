using Quizzer.DataModels.Enumerations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(MultipleChoiceQuestion), Schema = "question")]
    public sealed class MultipleChoiceQuestion : QuestionBase
    {
        public MultipleChoiceQuestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.MultipleChoice;
            WarnOnResultStep = false;
            UseRandomSequenceOnNonFinishSteps = true;
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