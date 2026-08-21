using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Eigenschaften-Frage: Hinweise werden schrittweise aufgedeckt, und die Punkte sinken
    /// proportional mit jedem aufgedeckten Schritt (<c>UseProportionalScoreReductionOnStep = true</c>).
    /// Spieler buzzen und antworten mündlich; Schritte werden numerisch beschriftet.
    /// Gespeichert in <c>question.PropertiesQuestion</c>.
    /// </summary>
    [Table(nameof(PropertiesQuestion), Schema = "question")]
    public sealed class PropertiesQuestion : QuestionBase
    {
        /// <summary>
        /// Initialisiert eine neue Eigenschaften-Frage mit Standardwerten:
        /// 100 Punkte, proportionale Punktereduktion, Buzzer-Layout, numerische Schrittnummerierung,
        /// vertikale Anzeige.
        /// </summary>
        public PropertiesQuestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.Properties;
            WarnOnResultStep = false;

            // Alles Typeigene kommt aus dem Profil - das ist die einzige Stelle,
            // an der es steht, und die Eingabemaske liest von dort ebenfalls.
            QuestionTypeProfiles.For(QuestionType.Properties).ApplyTo(this);
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new PropertiesQuestion();
        }
    }
}