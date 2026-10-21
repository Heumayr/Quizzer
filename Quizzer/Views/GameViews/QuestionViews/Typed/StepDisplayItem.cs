using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.StaticRessources;
using System.Windows;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.QuestionViews.Typed
{
    public sealed class StepDisplayItem
    {
        private readonly QuestionStepViewContext owner;

        public StepDisplayItem(
            QuestionStepViewContext owner,
            QuestionStepResource step,
            bool isVisibleSlot)
        {
            this.owner = owner;
            Step = step;
            IsVisibleSlot = isVisibleSlot;
        }

        public QuestionStepResource Step { get; }

        public bool IsVisibleSlot { get; }

        public Visibility SlotVisibility => Visibility.Visible;
        public double SlotOpacity => IsVisibleSlot ? 1.0 : 0.0;
        public bool SlotHitTestVisible => IsVisibleSlot;

        public string QuestionViewKey =>
            IsVisibleSlot ? Step.QuestionViewKey : string.Empty;

        public string QuestionViewKeyTyped =>
            IsVisibleSlot ? GetViewKeyTyped() : string.Empty;

        private string GetViewKeyTyped()
        {
            switch (owner.Question?.QuestionViewKeyType)
            {
                case QuestionViewKeyType.Alphabetical:
                    return $"{Step.QuestionViewKey}:";

                case QuestionViewKeyType.Numerical:
                    return $"{Step.QuestionViewKey}.";

                default:
                    return Step.QuestionViewKey;
            }
        }

        public string DisplayText
        {
            get
            {
                if (!IsVisibleSlot)
                    return string.Empty;

                if (!string.IsNullOrWhiteSpace(Step.StepText))
                    return Step.StepText;

                return Step.Designation ?? string.Empty;
            }
        }

        public bool HasText =>
            IsVisibleSlot && !string.IsNullOrWhiteSpace(DisplayText);

        public bool HasResource =>
            IsVisibleSlot &&
            Step.ResourceTyp != ResourceType.None &&
            !string.IsNullOrWhiteSpace(Step.ResourceFileName);

        public Visibility HasResourceVisibility =>
            HasResource ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TextOnlyVisibility =>
            HasText && !HasResource ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MediaOnlyVisibility =>
            !HasText && HasResource ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TextAndMediaVisibility =>
            HasText && HasResource ? Visibility.Visible : Visibility.Collapsed;

        public Brush GridBackgroundBrush => ThemeBrushes.Current.Auswahlfeld;
        public Brush GridBackgroundResultBrush => ThemeBrushes.Current.AuswahlfeldRichtig;
        public Brush HorizontalBackgroundBrush => ThemeBrushes.Current.Schrittleiste;

        public Brush GridBackground =>
            IsVisibleSlot && ZeigtRichtig
                ? GridBackgroundResultBrush
                : GridBackgroundBrush;

        // Bei der Reihenfolge bedeuten Haekchen nichts - eine umgewandelte Multiple-Choice-Frage
        // traegt sie aber noch, und dann leuchteten zufaellige Felder als "richtig".
        private bool ZeigtRichtig =>
            Step.IsResult
            && owner.Question?.Typ is not (QuestionType.Ordering or QuestionType.Matching)
            && (owner.IsMasterView || owner.Step?.IsFinish == true);

        /// <summary>
        /// Reihenfolgefrage: der richtige Platz dieses Elements - beim Spielleiter immer, auf dem
        /// Beamer erst bei der Auflösung. Die Tasten mischt das Spiel; der Platz bleibt.
        /// </summary>
        public string SolutionBadge =>
            IsVisibleSlot
            && owner.Question?.Typ is QuestionType.Ordering or QuestionType.Matching
            && Step.SolutionNumber > 0
            && (owner.IsMasterView || owner.Step?.IsFinish == true)
                ? owner.Question?.Typ == QuestionType.Matching
                    ? $"zu {Step.SolutionNumber}"
                    : $"Platz {Step.SolutionNumber}"
                : string.Empty;

        public Visibility SolutionBadgeVisibility =>
            string.IsNullOrEmpty(SolutionBadge) ? Visibility.Collapsed : Visibility.Visible;

        public Brush VerticalBackground =>
           IsVisibleSlot && ZeigtRichtig
               ? Brushes.Transparent
               : HorizontalBackgroundBrush;
    }
}