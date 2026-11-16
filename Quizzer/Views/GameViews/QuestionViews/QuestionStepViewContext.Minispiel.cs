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
        /// <summary>Der Regeltext der Art und Spielweise - leer bei allen anderen Typen.</summary>
        public string MinispielRegeltext
            => Question is MinigameQuestion minispiel ? MinigameProfiles.RulesFor(minispiel) : string.Empty;

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
