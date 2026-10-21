using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Reihenfolgefrage (M12, 2026-10-21): die Schritte sind <b>Elemente</b>, und die Lösung ist
    /// ihre Reihenfolge. Am Telefon werden sie gemischt angeboten und der Reihe nach angetippt.
    /// <para>
    /// <b>Keine eigenen Spalten.</b> Die Lösung steht je Schritt in
    /// <see cref="Base.QuestionStepResource.SolutionNumber"/> - dort, wo auch die übrigen Angaben
    /// eines Elements stehen, und so, dass Export und Abgleich sie ohne Zusatz mitnehmen.
    /// </para>
    /// </summary>
    [Table(nameof(OrderingQuestion), Schema = "question")]
    public sealed class OrderingQuestion : QuestionBase
    {
        public OrderingQuestion()
        {
            Points = 100;
            MinusPoints = 0;
            Typ = QuestionType.Ordering;
            WarnOnResultStep = false;

            // Alles Typeigene kommt aus dem Profil - das ist die einzige Stelle, an der es
            // steht, und die Eingabemaske liest von dort ebenfalls.
            QuestionTypeProfiles.For(QuestionType.Ordering).ApplyTo(this);
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new OrderingQuestion();
        }
    }
}
