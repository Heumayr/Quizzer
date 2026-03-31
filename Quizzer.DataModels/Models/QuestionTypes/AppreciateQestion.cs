using Quizzer.DataModels.Enumerations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(AppreciateQestion), Schema = "question")]
    public class AppreciateQestion : QuestionBase
    {
        public AppreciateQestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.Appreciate;
            WarnOnResultStep = false;
            UseRandomSequenceOnNonFinishSteps = false;
            DefaultFinishType = FinishType.AllPreviousSteps;
            BuzzerControlsLayout = BuzzerControlsLayout.Input;
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new AppreciateQestion();
        }
    }
}