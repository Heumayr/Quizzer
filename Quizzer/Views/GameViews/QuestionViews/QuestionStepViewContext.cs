using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Quizzer.Views.GameViews.QuestionViews
{
    public class QuestionStepViewContext : UcViewModelBase
    {
        // clone
        public CurrentQuestionViewModel Owner { get; set; } = null!;

        public QuestionStepResource? Step { get; set; }

        public bool IsMasterView
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MasterViewVisibility));
            }
        } = false;

        public Visibility MasterViewVisibility => IsMasterView ? Visibility.Visible : Visibility.Collapsed;

        //end Clone

        public string ResourceRootFolder => Settings.ResourceRootFolder;
        public string AudioPlaceholderFile => Settings.AudioPlaceholderFile;

        public QuestionBase? Question => Owner.Question;

        public QuestionStepResource[] AllSteps => Owner.QuestionOrderedSteps;

        public QuestionStepResource[] PreviousSteps =>
            Step == null
                ? []
                : Owner.QuestionOrderedSteps
                    .Where(s => s.SequenceNumber < Step.SequenceNumber)
                    .OrderBy(s => s.SequenceNumber)
                    .ToArray();

        public QuestionStepResource[] NextSteps =>
            Step == null
                ? []
                : Owner.QuestionOrderedSteps
                    .Where(s => s.SequenceNumber > Step.SequenceNumber)
                    .OrderBy(s => s.SequenceNumber)
                    .ToArray();

        public QuestionType QuestionType => Question?.Typ ?? default;

        public static QuestionStepViewContext CloneForDifferentView(QuestionStepViewContext original, bool isMasterView)
        {
            return new QuestionStepViewContext
            {
                Owner = original.Owner,
                Step = original.Step,
                IsMasterView = isMasterView
            };
        }
    }
}