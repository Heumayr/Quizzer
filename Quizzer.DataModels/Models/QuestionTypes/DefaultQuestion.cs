using Quizzer.DataModels.Enumerations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(DefaultQuestion), Schema = "question")]
    public sealed class DefaultQuestion : QuestionBase
    {
        public DefaultQuestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.Default;
            WarnOnResultStep = false;
            UseRandomSequenceOnNoneFinishSteps = false;
            DefaultFinishType = FinishType.AllPreviousSteps;
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new DefaultQuestion();
        }
    }
}