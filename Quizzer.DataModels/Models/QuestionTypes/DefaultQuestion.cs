using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Standardfrage: Der Spielleiter liest eine Frage vor; der erste Spieler, der buzzt,
    /// darf antworten. Schritte werden in fixer Reihenfolge abgespielt.
    /// Verwendet das klassische Buzzer-Layout. Gespeichert in <c>question.DefaultQuestion</c>.
    /// </summary>
    [Table(nameof(DefaultQuestion), Schema = "question")]
    public sealed class DefaultQuestion : QuestionBase
    {
        /// <summary>
        /// Initialisiert eine neue Standardfrage mit Standardwerten:
        /// 100 Punkte, 100 Minuspunkte, Buzzer-Layout, keine zufällige Schrittfolge.
        /// </summary>
        public DefaultQuestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.Default;
            WarnOnResultStep = false;

            // Alles Typeigene kommt aus dem Profil - das ist die einzige Stelle,
            // an der es steht, und die Eingabemaske liest von dort ebenfalls.
            QuestionTypeProfiles.For(QuestionType.Default).ApplyTo(this);
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new DefaultQuestion();
        }
    }
}