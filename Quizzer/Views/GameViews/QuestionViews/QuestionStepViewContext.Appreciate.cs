using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Windows;

namespace Quizzer.Views.GameViews.QuestionViews
{
    /// <summary>
    /// Schaetzfrage-Teil der Schrittanzeige: was geschaetzt wird und - zum Schluss - welcher
    /// Wert richtig gewesen waere.
    /// </summary>
    public partial class QuestionStepViewContext
    {
        /// <summary>Die Frage als Schaetzfrage, sofern es eine ist.</summary>
        public AppreciateQestion? AppreciateQuestion => Question as AppreciateQestion;

        /// <summary>
        /// Der Sollwert im Klartext. Wird erst am Abschlussschritt fuer alle sichtbar - vorher
        /// nur in der Spielleiteransicht, sonst stuende die Antwort auf dem Beamer.
        /// </summary>
        public string AppreciateExpectedText
            => AppreciateQuestion == null
                ? string.Empty
                : AppreciateEvaluator.DescribeExpected(AppreciateQuestion);

        /// <summary>Was die Spieler eintippen sollen, im Klartext.</summary>
        public string AppreciateAskText
        {
            get
            {
                if (AppreciateQuestion == null)
                    return string.Empty;

                var placeholder = AppreciateEvaluator.PlaceholderFor(
                    AppreciateQuestion.ValueKind, AppreciateQuestion.Unit);

                return $"Schätzt am Telefon: {placeholder}";
            }
        }

        /// <summary>
        /// Ob der Sollwert angezeigt werden darf: in der Spielleiteransicht immer, auf dem
        /// Spielerbildschirm erst beim Abschlussschritt.
        /// </summary>
        public Visibility AppreciateExpectedVisibility
            => AppreciateQuestion != null && (IsMasterView || (Step?.IsFinish ?? false))
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>Die Aufforderung zum Schaetzen - solange die Aufloesung noch aussteht.</summary>
        public Visibility AppreciateAskVisibility
            => AppreciateQuestion != null && !(Step?.IsFinish ?? false)
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>
        /// Meldet die abgeleiteten Werte als geaendert. Wird aus den Settern von Step und
        /// IsMasterView aufgerufen - beide entscheiden mit, ob der Sollwert gezeigt wird.
        /// </summary>
        private void RaiseAppreciateChanged()
        {
            OnPropertyChanged(nameof(AppreciateQuestion));
            OnPropertyChanged(nameof(AppreciateExpectedText));
            OnPropertyChanged(nameof(AppreciateAskText));
            OnPropertyChanged(nameof(AppreciateExpectedVisibility));
            OnPropertyChanged(nameof(AppreciateAskVisibility));
        }
    }
}
