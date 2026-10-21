using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Wahr oder falsch (M12, 2026-10-21): die Schritte sind <b>Aussagen</b>, und
    /// <see cref="Base.QuestionStepResource.IsResult"/> heißt hier „wahr".
    /// <para>
    /// <b>Keine eigenen Spalten</b> - der Haken, den Multiple Choice für „richtige Antwort" führt,
    /// trägt hier dieselbe Information für jede Aussage.
    /// </para>
    /// </summary>
    [Table(nameof(TrueFalseQuestion), Schema = "question")]
    public sealed class TrueFalseQuestion : QuestionBase
    {
        public TrueFalseQuestion()
        {
            Points = 100;
            MinusPoints = 0;
            Typ = QuestionType.TrueFalse;
            WarnOnResultStep = false;

            QuestionTypeProfiles.For(QuestionType.TrueFalse).ApplyTo(this);
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new TrueFalseQuestion();
        }
    }
}
