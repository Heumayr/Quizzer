using Quizzer.DataModels.Enumerations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(PropertiesQuestion), Schema = "question")]
    public sealed class PropertiesQuestion : QuestionBase
    {
        public PropertiesQuestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.Properties;
            WarnOnResultStep = false;
            UseRandomSequenceOnNonFinishSteps = false;
            UseProportionalPointsPerStep = true;
            DefaultFinishType = FinishType.AllPreviousSteps;
            BuzzerControlsLayout = BuzzerControlsLayout.Buzzer;
            StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical;
            QuestionViewKeyType = QuestionViewKeyType.Numerical;
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new PropertiesQuestion();
        }
    }
}