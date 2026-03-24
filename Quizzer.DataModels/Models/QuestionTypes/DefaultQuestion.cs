using Quizzer.DataModels.Enumerations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(DefaultQuestion), Schema = "question")]
    public class DefaultQuestion : QuestionBase
    {
        public override int Points { get; set; } = 100;
        public override int MinusPoints { get; set; } = 100;
        public override QuestionType Typ { get; protected set; } = QuestionType.Default;

        public override bool WarnOnResultStep { get; set; } = false;
        public override bool UseRandomSequenceOnNonFinishSteps { get; set; } = false;

        public override FinishType DefaultFinishType { get; protected set; } = FinishType.AllPreviousSteps;

        protected override QuestionBase CreateCloneInstance()
        {
            return new DefaultQuestion();
        }
    }
}