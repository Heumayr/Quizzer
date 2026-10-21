using Quizzer.DataModels.Enumerations;
using System.Windows;

namespace Quizzer.Views.GameViews.QuestionViews
{
    /// <summary>
    /// Zuordnung (M12): die festen Begriffe links, nummeriert in der Folge der Maske. Die
    /// Gegenstücke stehen gemischt im Raster daneben - dasselbe Raster wie bei Multiple Choice.
    /// </summary>
    public partial class QuestionStepViewContext
    {
        /// <summary>Die Begriffe als „1  Österreich" - in der Folge ihrer Nummern.</summary>
        public string[] ZuordnungBegriffe =>
            Question?.Typ == QuestionType.Matching
                ? Question.Steps
                    .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly && s.SolutionNumber > 0)
                    .OrderBy(s => s.SolutionNumber)
                    .Select(s => $"{s.SolutionNumber}   {s.PartnerText}")
                    .ToArray()
                : [];

        /// <summary>
        /// Sichtbar, sobald das erste Gegenstück steht - auf dem Startbildschirm nicht: dort darf
        /// noch nichts zu lesen sein, solange der Spielleiter vorliest.
        /// </summary>
        public Visibility ZuordnungVisibility =>
            Question?.Typ == QuestionType.Matching && DisplaySteps.Any(d => d.IsVisibleSlot)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
