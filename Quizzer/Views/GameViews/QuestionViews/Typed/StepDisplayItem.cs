using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Text;
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

        // Hidden reserviert Platz, rendert aber nichts
        public Visibility SlotVisibility =>
            IsVisibleSlot ? Visibility.Visible : Visibility.Collapsed;

        public string QuestionViewKey =>
            IsVisibleSlot ? Step.QuestionViewKey : string.Empty;

        public string DisplayText =>
            IsVisibleSlot
                ? (!string.IsNullOrWhiteSpace(Step.StepText)
                    ? Step.StepText
                    : Step.Designation)
                : string.Empty;

        public bool HasResource =>
            IsVisibleSlot &&
            Step.ResourceTyp != ResourceType.None &&
            !string.IsNullOrWhiteSpace(Step.ResourceFileName);

        public Visibility HasResourceVisibility =>
            HasResource ? Visibility.Visible : Visibility.Collapsed;

        public GridLength MediaColumnWidth =>
            HasResource
                ? new GridLength(1, GridUnitType.Auto)
                : new GridLength(0);

        public Brush ChoiceBackgroundBrush => StaticResources.ChoiceBackgroundImageBrush;
        public Brush ChoiceBackgroundResultBrush => StaticResources.ChoiceBackgroundResultImageBrush;

        public Brush RowBackground =>
            IsVisibleSlot && ((owner.IsMasterView && Step.IsResult) || owner.Step?.IsFinish == true && Step.IsResult)
                ? ChoiceBackgroundResultBrush
                : ChoiceBackgroundBrush;
    }
}