using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Zuordnungsfrage (M12, 2026-10-21): jeder Schritt ist ein Paar - links der feste Begriff
    /// (<see cref="Base.QuestionStepResource.PartnerText"/>), rechts das Gegenstück (Text oder
    /// Bild), das gemischt mit einer Taste erscheint.
    /// <para>
    /// <b>Keine eigenen Spalten.</b> Die Nummer des Begriffs steht in
    /// <see cref="Base.QuestionStepResource.SolutionNumber"/>, wie bei der Reihenfolge.
    /// </para>
    /// </summary>
    [Table(nameof(MatchingQuestion), Schema = "question")]
    public sealed class MatchingQuestion : QuestionBase
    {
        public MatchingQuestion()
        {
            Points = 100;
            MinusPoints = 0;
            Typ = QuestionType.Matching;
            WarnOnResultStep = false;

            QuestionTypeProfiles.For(QuestionType.Matching).ApplyTo(this);
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new MatchingQuestion();
        }
    }
}
