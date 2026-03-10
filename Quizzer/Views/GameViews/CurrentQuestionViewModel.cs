using LocalBuzzer.Service;
using Newtonsoft.Json.Linq;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Extentions;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.GameViews
{
    public class CurrentQuestionViewModel : ViewModelBase
    {
        public GamePlayerViewModel? GamePlayerViewModel { get; set; }

        public CurrentQuestionViewModel()
        {
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        protected override async Task OnloadAsync()
        {
            OnPlayerConnectionStateChanged(this, StaticManager.BuzzerServerViewModel.ServerState);

            if (Coordinate == null)
                return;

            if (Coordinate.QuestionBaseId == Guid.Empty)
            {
                MessageBox.Show("No question set");
                return;
            }

            //reload question
            using var ctrlQ = new QuestionBasesController();
            Coordinate.QuestionBase = (await ctrlQ.GetAsync(Coordinate.QuestionBaseId));

            NextStep = Coordinate.QuestionBase?.OrderdSteps.FirstOrDefault();

            //TODO: Load Results

            await OnModeldChanged();
        }

        private async Task OnModeldChanged()
        {
            OnPropertyChanged(nameof(QuestionType));
            OnPropertyChanged(nameof(QuestionDesignation));
            OnPropertyChanged(nameof(QuestionDesignationShort));
            OnPropertyChanged(nameof(QuestionCategory));
            OnPropertyChanged(nameof(QuestionDifficulty));
            OnPropertyChanged(nameof(Phase));
            OnPropertyChanged(nameof(CurrentPoints));
            OnPropertyChanged(nameof(CurrentMinusPoints));
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(QuestionSteps));
            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(NextStep));
        }

        protected override Task OnClosed()
        {
            GamePlayerViewModel?.SetView(null);

            return base.OnClosed();
        }

        public Brush BackgroundBrush
        {
            get => backgroundBrush;
            set
            {
                backgroundBrush = value;
                OnPropertyChanged();
            }
        }

        private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        {
            BackgroundBrush = e switch
            {
                ServerState.None => Brushes.White,
                ServerState.Running => Brushes.Red,
                ServerState.Stopped => Brushes.Wheat,
                ServerState.AllConnected => Brushes.WhiteSmoke,
                _ => Brushes.White
            };

            BuzzerState = e.DescriptionOrString();
        }

        public GameGridCoordinate? Coordinate { get; set; }

        public QuestionBase? Question => Coordinate?.QuestionBase;

        public BuzzerServerViewModel? BuzzerServerViewModel => StaticRessources.StaticManager.BuzzerServerViewModel;

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }

        public string QuestionType => Question?.Typ.DescriptionOrString() ?? String.Empty;
        public string QuestionDesignation => Question?.Designation ?? String.Empty;
        public string QuestionDesignationShort => Question?.DesignationShort ?? String.Empty;
        public string QuestionCategory => Question?.Category?.Designation ?? String.Empty;
        public string QuestionDifficulty => Question?.Difficulty.DescriptionOrString().ToString() ?? String.Empty;
        public int Phase => Coordinate?.Phase ?? 0;
        public int CurrentPoints => Coordinate?.CurrentPoints ?? 0;
        public int CurrentMinusPoints => Coordinate?.CurrentMinusPoints ?? 0;
        public string Notes => Question?.Notes ?? String.Empty;

        public QuestionStepResource[] QuestionSteps => Question?.Steps.OrderBy(s => s.IsResult).ThenBy(s => s.SquenceNumber).ToArray() ?? [];

        private QuestionStepResource? currentStep;

        public QuestionStepResource? CurrentStep
        {
            get => currentStep;
            set
            {
                currentStep = value;
                OnPropertyChanged();
                GamePlayerViewModel?.SetView(value);
            }
        }

        private QuestionStepResource? nextStep;

        public QuestionStepResource? NextStep
        {
            get => nextStep;
            set
            {
                nextStep = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<QuestionStepResource> SelectedSteps { get; set; } = new();

        private AsyncRelayCommand? _startStepCommand;
        public AsyncRelayCommand? StartStepCommnad => _startStepCommand ??= new AsyncRelayCommand(StartStepAsync);

        private async Task StartStepAsync(object? arg)
        {
            CurrentStep = Question?.OrderdSteps.FirstOrDefault();
            NextStep = Question?.GetNextStep(CurrentStep);
        }

        private AsyncRelayCommand? _nextStepCommand;
        public AsyncRelayCommand? NextStepCommnad => _nextStepCommand ??= new AsyncRelayCommand(NextStepAsync);

        private async Task NextStepAsync(object? arg)
        {
            SetStepsView(CurrentStep);
        }

        private void SetStepsView(QuestionStepResource? currentStep, bool up = true)
        {
            if (Question == null)
            {
                MessageBox.Show("No Question set");
                return;
            }

            if (up)
            {
                if (CurrentStep == Question.OrderdSteps.Last())
                {
                    //MessageBox.Show("End reached");
                    return;
                }

                if (Question.WarnOnResultStep
                    && ((NextStep?.IsResult ?? false) && (!CurrentStep?.IsResult ?? true))
                    && MessageBox.Show("Next is result! Want to proceed?", "Result step ahead", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }

                CurrentStep = NextStep;
                NextStep = Question.GetNextStep(CurrentStep);
            }
            else
            {
                if (CurrentStep == Question.OrderdSteps.First())
                {
                    //MessageBox.Show("Start reached");
                    return;
                }

                NextStep = CurrentStep;
                CurrentStep = Question.GetStepBehind(CurrentStep);
            }
        }

        private AsyncRelayCommand? _backStepCommand;
        public AsyncRelayCommand? BackStepCommnad => _backStepCommand ??= new AsyncRelayCommand(BackStepAsync);

        private async Task BackStepAsync(object? arg)
        {
            SetStepsView(CurrentStep, false);
        }

        private AsyncRelayCommand? _openStepCommand;

        public AsyncRelayCommand? OpenStepCommand => _openStepCommand ??= new AsyncRelayCommand(OpenStepAsync);

        private async Task OpenStepAsync(object? arg)
        {
            if (Question == null)
            {
                MessageBox.Show("No Question set");
                return;
            }

            var first = SelectedSteps?.FirstOrDefault();

            if (first == currentStep) return;

            if (Question.WarnOnResultStep
                   && ((first?.IsResult ?? false) && (!CurrentStep?.IsResult ?? true))
                   && MessageBox.Show("Next is result! Want to proceed?", "Result step ahead", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            CurrentStep = first;
            NextStep = Question.GetNextStep(CurrentStep);
        }

        #region Buzzer

        private string buzzerState = string.Empty;
        private Brush backgroundBrush = Brushes.Wheat;

        public string BuzzerState
        {
            get => buzzerState;
            set
            {
                buzzerState = value;
                OnPropertyChanged();
            }
        }

        public ICommand? ResetRoundCommand => BuzzerServerViewModel?.ResetRoundCommand;

        #endregion Buzzer
    }
}