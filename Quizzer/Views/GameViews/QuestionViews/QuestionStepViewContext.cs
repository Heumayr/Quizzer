using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using Quizzer.Views.StaticRessources;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.QuestionViews
{
    public class QuestionStepViewContext : UcViewModelBase
    {
        private CurrentQuestionViewModel owner = null!;

        public CurrentQuestionViewModel Owner
        {
            get => owner;
            set
            {
                if (ReferenceEquals(owner, value))
                    return;

                owner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Question));
                OnPropertyChanged(nameof(AllSteps));
                OnPropertyChanged(nameof(LayoutReferenceSteps));
                OnPropertyChanged(nameof(MaxDisplayStepCount));

                NotifyDisplayStepLayoutChanged();
            }
        }

        private QuestionStepResource? step;

        public QuestionStepResource? Step
        {
            get => step;
            set
            {
                step = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStepTextVisibility));
                OnPropertyChanged(nameof(HasResourceVisibility));

                OnPropertyChanged(nameof(FinishColumnColumnWidthFinishStep));
                OnPropertyChanged(nameof(FinishColumnColumnWidthSteps));

                OnPropertyChanged(nameof(StepTextColumnWidth));
                OnPropertyChanged(nameof(ResourceColumnWidth));

                OnPropertyChanged(nameof(StepTextRowHeight));
                OnPropertyChanged(nameof(ResourceRowHeight));

                OnPropertyChanged(nameof(PreviousSteps));
                OnPropertyChanged(nameof(NextSteps));

                OnPropertyChanged(nameof(TextForgroundBrush));
                OnPropertyChanged(nameof(TextBackgroundBrush));

                NotifyDisplayStepLayoutChanged();
            }
        }

        public bool IsMasterView
        {
            get => field;
            set
            {
                if (field == value)
                    return;

                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MasterViewVisibility));
                OnPropertyChanged(nameof(FillAvailableVisibility));
                OnPropertyChanged(nameof(EmbeddedScaledVisibility));

                NotifyDisplayStepLayoutChanged();
            }
        } = false;


        public Brush TextForgroundBrush => Step?.IsResult == true ? Brushes.Wheat : Brushes.WhiteSmoke;
        public Brush TextBackgroundBrush => Step?.IsResult == true ? Brushes.Black : Brushes.Transparent;

        public Visibility FillAvailableVisibility =>
            IsMasterView ? Visibility.Collapsed : Visibility.Visible;

        public Visibility EmbeddedScaledVisibility =>
            IsMasterView ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MasterViewVisibility =>
            IsMasterView ? Visibility.Visible : Visibility.Collapsed;

        public bool HasStepText =>
            !string.IsNullOrWhiteSpace(Step?.StepText);

        public Visibility HasStepTextVisibility =>
            HasStepText ? Visibility.Visible : Visibility.Collapsed;

        public bool HasResource =>
            Step != null &&
            Step.ResourceTyp != ResourceType.None &&
            !string.IsNullOrWhiteSpace(Step.ResourceFileName);

        public Visibility HasResourceVisibility =>
            HasResource ? Visibility.Visible : Visibility.Collapsed;


        public GridLength FinishColumnColumnWidthSteps
        {
            get
            {
                if (Step?.IsFinish == true)
                    return new GridLength(2, GridUnitType.Star);

                return new GridLength(1, GridUnitType.Star);    
            }
        }

        public GridLength FinishColumnColumnWidthFinishStep
        {
            get
            {
                if (Step?.IsFinish == true)
                    return new GridLength(3, GridUnitType.Star);

                return new GridLength(0);
            }
        }


        public GridLength StepTextColumnWidth
        {
            get
            {
                if (HasStepText && HasResource)
                    return new GridLength(1, GridUnitType.Star);

                if (HasStepText)
                    return new GridLength(1, GridUnitType.Star);

                return new GridLength(0);
            }
        }

        public GridLength ResourceColumnWidth
        {
            get
            {
                if (HasStepText && HasResource)
                    return new GridLength(1, GridUnitType.Auto);

                if (HasResource)
                    return new GridLength(1, GridUnitType.Star);

                return new GridLength(0);
            }
        }

        public GridLength StepTextRowHeight
        {
            get
            {
                if (HasStepText && HasResource)
                    return new GridLength(1, GridUnitType.Star);

                if (HasStepText)
                    return new GridLength(1, GridUnitType.Star);

                return new GridLength(0);
            }
        }

        public GridLength ResourceRowHeight
        {
            get
            {
                if (HasStepText && HasResource)
                    return new GridLength(5, GridUnitType.Star);

                if (HasResource)
                    return new GridLength(1, GridUnitType.Star);

                return new GridLength(0);
            }
        }

        public Visibility DesignationVisibility =>
            string.IsNullOrWhiteSpace(Step?.Designation)
                ? Visibility.Collapsed
                : Visibility.Visible;

        public string ResourceRootFolder => Settings.ResourceRootFolder;
        public string AudioPlaceholderFile => Settings.AudioPlaceholderFile;

        public QuestionBase? Question => Owner?.Question;

        public QuestionStepResource[] AllSteps =>
            Owner?.QuestionOrderedSteps ?? [];

        public QuestionStepResource[] LayoutReferenceSteps =>
            AllSteps
                .Where(s => !s.IsStart && !s.IsFinish)
                .OrderBy(s => s.SequenceNumber)
                .ToArray();

        public int MaxDisplayStepCount =>
            LayoutReferenceSteps.Length <= 0
                ? 1
                : LayoutReferenceSteps.Length;

        public QuestionStepResource[] PreviousSteps =>
            Step == null
                ? []
                : Owner.QuestionOrderedSteps
                    .Where(s => s.SequenceNumber < Step.SequenceNumber && !s.IsStart && !s.IsFinish)
                    .OrderBy(s => s.SequenceNumber)
                    .ToArray();

        public int PreviousStepsCount => PreviousSteps.Length;

        public QuestionStepResource[] NextSteps =>
            Step == null
                ? []
                : Owner.QuestionOrderedSteps
                    .Where(s => s.SequenceNumber > Step.SequenceNumber && !s.IsStart && !s.IsFinish)
                    .OrderBy(s => s.SequenceNumber)
                    .ToArray();

        public QuestionType QuestionType => Question?.Typ ?? default;

        private StepDisplayItem[] displaySteps = [];

        public StepDisplayItem[] DisplaySteps
        {
            get => displaySteps;
            private set
            {
                displaySteps = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayStepCount));
            }
        }

        private void RebuildDisplaySteps()
        {
            if (LayoutReferenceSteps.Length == 0)
            {
                DisplaySteps = [];
                return;
            }

            int currentSequence = Step?.SequenceNumber ?? int.MinValue;

            DisplaySteps = LayoutReferenceSteps
                .Select(s => new StepDisplayItem(
                    this,
                    s,
                    s.SequenceNumber <= currentSequence))
                .ToArray();
        }

        public StepDisplayLayoutMode DisplayLayoutMode
        {
            get => field;
            set
            {
                if (field == value)
                    return;

                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VerticalLayoutVisibility));
                OnPropertyChanged(nameof(HorizontalLayoutVisibility));
                OnPropertyChanged(nameof(GridLayoutVisibility));
                OnPropertyChanged(nameof(DisplayGridColumns));
                OnPropertyChanged(nameof(DisplayGridRows));
                OnPropertyChanged(nameof(VerticalLayoutRows));
                OnPropertyChanged(nameof(VerticalLayoutColumns));
                OnPropertyChanged(nameof(HorizontalLayoutRows));
                OnPropertyChanged(nameof(HorizontalLayoutColumns));
            }
        } = StepDisplayLayoutMode.Grid;

        public Visibility VerticalLayoutVisibility =>
            DisplayLayoutMode == StepDisplayLayoutMode.Vertical
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility HorizontalLayoutVisibility =>
            DisplayLayoutMode == StepDisplayLayoutMode.Horizontal
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility GridLayoutVisibility =>
            DisplayLayoutMode == StepDisplayLayoutMode.Grid
                ? Visibility.Visible
                : Visibility.Collapsed;

        public int DisplayStepCount => DisplaySteps.Length;

        public int VerticalLayoutRows => MaxDisplayStepCount;
        public int VerticalLayoutColumns => 1;

        public int HorizontalLayoutRows => 1;
        public int HorizontalLayoutColumns => MaxDisplayStepCount;

        public int DisplayGridColumns
        {
            get
            {
                var count = MaxDisplayStepCount;
                if (count <= 0)
                    return 1;

                return (int)System.Math.Ceiling(System.Math.Sqrt(count));
            }
        }

        public int DisplayGridRows
        {
            get
            {
                var count = MaxDisplayStepCount;
                var cols = DisplayGridColumns;

                if (count <= 0 || cols <= 0)
                    return 1;

                return (int)System.Math.Ceiling((double)count / cols);
            }
        }

        public QuestionStepResource? FinishStep =>
              Owner?.QuestionOrderedSteps?
               .FirstOrDefault(s => s.IsFinish);

        public StepDisplayItem? FinishDisplayItem =>
            FinishStep == null
                ? null
                : new StepDisplayItem(this, FinishStep, true);

        public Visibility FinishStepColumnVisibility =>
            Step?.IsFinish == true && FinishDisplayItem != null
                ? Visibility.Visible
                : Visibility.Collapsed;

        private void NotifyDisplayStepLayoutChanged()
        {
            OnPropertyChanged(nameof(LayoutReferenceSteps));
            OnPropertyChanged(nameof(MaxDisplayStepCount));

            RebuildDisplaySteps();

            OnPropertyChanged(nameof(VerticalLayoutRows));
            OnPropertyChanged(nameof(VerticalLayoutColumns));
            OnPropertyChanged(nameof(HorizontalLayoutRows));
            OnPropertyChanged(nameof(HorizontalLayoutColumns));
            OnPropertyChanged(nameof(DisplayGridColumns));
            OnPropertyChanged(nameof(DisplayGridRows));

            OnPropertyChanged(nameof(VerticalLayoutVisibility));
            OnPropertyChanged(nameof(HorizontalLayoutVisibility));
            OnPropertyChanged(nameof(GridLayoutVisibility));

            OnPropertyChanged(nameof(FinishStep));
            OnPropertyChanged(nameof(FinishDisplayItem));
            OnPropertyChanged(nameof(FinishStepColumnVisibility));
        }

        public static QuestionStepViewContext CloneForDifferentView(QuestionStepViewContext original, bool isMasterView, StepDisplayLayoutMode layoutMode)
        {
            return new QuestionStepViewContext
            {
                Owner = original.Owner,
                Step = original.Step,
                IsMasterView = isMasterView,
                DisplayLayoutMode = layoutMode
            };
        }
    }
}