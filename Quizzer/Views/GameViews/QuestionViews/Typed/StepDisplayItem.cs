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

        public Brush ChoiceBackgroundBrush => StaticResources.ChoiceBackgroundImageBrush;
        public Brush ChoiceBackgroundResultBrush => StaticResources.ChoiceBackgroundResultImageBrush;

        public Brush RowBackground =>
            IsVisibleSlot && ((owner.IsMasterView && Step.IsResult) || owner.Step?.IsFinish == true && Step.IsResult)
                ? ChoiceBackgroundResultBrush
                : ChoiceBackgroundBrush;
    }
}