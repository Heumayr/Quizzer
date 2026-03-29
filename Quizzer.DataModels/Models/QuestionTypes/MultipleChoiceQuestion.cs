using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(MultipleChoiceQuestion), Schema = "question")]
    public class MultipleChoiceQuestion : QuestionBase
    {
        public override int Points { get; set; } = 100;
        public override int MinusPoints { get; set; } = 100;
        public override QuestionType Typ { get; protected set; } = QuestionType.MultipleChoice;

        public override bool WarnOnResultStep { get; set; } = false;

        public override bool UseRandomSequenceOnNonFinishSteps { get; set; } = true;

        public override FinishType DefaultFinishType { get; protected set; } = FinishType.AllPreviousSteps;

        public override BuzzerControlsLayout BuzzerControlsLayout { get; set; } = BuzzerControlsLayout.KeySelect;

        public override StepDisplayLayoutMode StepDisplayLayoutMode { get; set; } = StepDisplayLayoutMode.Grid;

        protected override QuestionBase CreateCloneInstance()
        {
            return new MultipleChoiceQuestion();
        }
    }
}