using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Zeile Inhalt eines Minispiels: Aussage oder Begriff, samt Haken.</summary>
    /// <param name="Text">Was in der Zeile steht, getrimmt.</param>
    /// <param name="Wahr">Der Haken der Zeile - bei der Blitzrunde „wahr".</param>
    public sealed record MinispielZeile(string Text, bool Wahr);

    /// <summary>
    /// Der Inhalt eines Minispiels aus seinen Zeilen (M35 C5): Aussagen der Blitzrunde, Begriffe des
    /// Buchstabensalats. Fragefenster und Pruefung lesen von hier; der Server bekommt die Liste mit der
    /// Einstellung und behaelt sie fuer sich.
    /// </summary>
    public static class MinispielInhalt
    {
        /// <summary>Die gewoehnlichen Zeilen in ihrer Reihenfolge; ohne Text zaehlt eine Zeile nicht.</summary>
        public static IReadOnlyList<MinispielZeile> Zeilen(MinigameQuestion frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            return (frage.Steps ?? [])
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly)
                .OrderBy(s => s.SequenceNumber)
                .Select(s => new MinispielZeile(
                    (string.IsNullOrWhiteSpace(s.StepText) ? s.Designation ?? string.Empty : s.StepText).Trim(), s.IsResult))
                .Where(z => z.Text.Length > 0)
                .ToList();
        }
    }
}
