using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;

namespace Quizzer.Views.GameViews.QuestionViews
{
    /// <summary>
    /// Minispiel (M35 B2): der Regeltext ueber den Optionen - am Beamer und beim Spielleiter.
    /// Der Name der Art steht schon auf dem Aufdeckbildschirm (<c>MinigameQuestion.TypDisplayName</c>).
    /// </summary>
    public partial class QuestionStepViewContext
    {
        /// <summary>
        /// Ob die Art im Minispiel-Rahmen laeuft (Reaktion M35 B3, Tippsprint und Stoppuhr C1) - dann zeigt
        /// der Beamer Runden und Tafel statt der Optionen.
        /// </summary>
        public bool ZeigtMinispielTafel
            => Question is MinigameQuestion minispiel
               && MinigameProfiles.TryFor(minispiel.Kind, out var art)
               && art.Layout == DataModels.Enumerations.BuzzerControlsLayout.Minigame;

        /// <summary>„Runde n von m" und die Tafel - fuer beide Bildschirme dasselbe Objekt am Owner.</summary>
        public Sub.ReaktionAnzeige? Reaktion => Owner?.Reaktion;

        /// <summary>Der Regeltext der Art und Spielweise, im Teamspiel samt Teamwertung (M37 T8) - leer bei allen anderen Typen.</summary>
        public string MinispielRegeltext
            => Question is MinigameQuestion minispiel ? MinigameProfiles.RulesFor(minispiel, Owner?.Coordinate?.Game) : string.Empty;

        /// <summary>
        /// Sichtbar, sobald die erste Option steht - wie die Begriffe der Zuordnung, und aus demselben
        /// Grund: auf dem Startbildschirm darf nichts zu lesen sein, solange vorgelesen wird.
        /// </summary>
        public Visibility MinispielRegelVisibility
            => !string.IsNullOrEmpty(MinispielRegeltext) && DisplaySteps.Any(d => d.IsVisibleSlot)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
