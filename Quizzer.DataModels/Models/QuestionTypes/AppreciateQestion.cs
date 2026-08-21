using Quizzer.DataModels.Enumerations;
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
            UseRandomSequenceOnNoneFinishSteps = false;
            DefaultFinishType = FinishType.AllPreviousSteps;
            BuzzerControlsLayout = BuzzerControlsLayout.Input;
        }

        protected override QuestionBase CreateCloneInstance()
        {
            return new AppreciateQestion();
        }
    }
}