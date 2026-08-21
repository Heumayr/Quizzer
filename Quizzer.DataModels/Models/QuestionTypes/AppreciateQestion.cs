using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Schätzfrage: Spieler geben einen freien Textwert über das Input-Layout ein
    /// (z.B. Jahreszahl, Menge). Die Antworten werden vom Spielleiter bewertet.
    /// Gespeichert in <c>question.AppreciateQestion</c>.
    /// </summary>
    [Table(nameof(AppreciateQestion), Schema = "question")]
    public class AppreciateQestion : QuestionBase
    {
        /// <summary>
        /// Initialisiert eine neue Schätzfrage mit Standardwerten:
        /// 100 Punkte, Input-Buzzer-Layout, keine zufällige Schrittfolge.
        /// </summary>
        public AppreciateQestion()
        {
            Points = 100;
            MinusPoints = 100;
            Typ = QuestionType.Appreciate;
            WarnOnResultStep = false;

            // Alles Typeigene kommt aus dem Profil - das ist die einzige Stelle,
            // an der es steht, und die Eingabemaske liest von dort ebenfalls.
            QuestionTypeProfiles.For(QuestionType.Appreciate).ApplyTo(this);
        }

        /// <summary>
        /// Was geschaetzt wird. Bestimmt die Eingabeart am Telefon und die Einheitenauswahl.
        /// </summary>
        public AppreciateValueKind ValueKind { get; set; } = AppreciateValueKind.Number;

        /// <summary>Die Einheit des Sollwerts. Muss zu <see cref="ValueKind"/> passen.</summary>
        public AppreciateUnit Unit { get; set; } = AppreciateUnit.Stueck;

        /// <summary>
        /// Der richtige Wert, in der gewaehlten Einheit. Wird bei allen Arten ausser
        /// <see cref="AppreciateValueKind.Date"/> verwendet.
        /// </summary>
        public double ExpectedValue { get; set; }

        /// <summary>
        /// Das richtige Datum. Nur bei <see cref="AppreciateValueKind.Date"/> belegt;
        /// der Abstand zum Tipp wird dann in Tagen gerechnet.
        /// </summary>
        public DateTime? ExpectedDate { get; set; }

        protected override QuestionBase CreateCloneInstance()
        {
            return new AppreciateQestion
            {
                ValueKind = ValueKind,
                Unit = Unit,
                ExpectedValue = ExpectedValue,
                ExpectedDate = ExpectedDate,
            };
        }
    }
}