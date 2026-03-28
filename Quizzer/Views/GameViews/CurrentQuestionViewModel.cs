using LocalBuzzer.Service;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Extentions;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.GameViews
{
    public class CurrentQuestionViewModel : ViewModelBase
    {
        public GamePlayerViewModel? GamePlayerViewModel { get; set; }

        public PlayersResultViewModel? PlayersResultViewModel { get; set; }

        public BuzzerControlsViewModel? BuzzerControlsViewModel => StaticManager.BuzzerServerViewModel.BuzzerControlsViewModel;

        private string backgroundImagePath = Settings.BackgroundImagePath;
        public Brush HeaderBrush { get; set; } = Brushes.Black;

        public string BackgroundImagePath
        {
            get => backgroundImagePath;
            set
            {
                backgroundImagePath = value;
                OnPropertyChanged();
            }
        }

        public CurrentQuestionViewModel()
        {
            //StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        protected override async Task OnloadAsync()
        {
            //OnPlayerConnectionStateChanged(this, StaticManager.BuzzerServerViewModel.ServerState);

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

            Coordinate.QuestionBase?.CalculateOrderdSteps();

            NextStep = Coordinate.QuestionBase?.OrderedSteps.FirstOrDefault();
            FinishStep = Coordinate.QuestionBase?.OrderedSteps.FirstOrDefault(s => s.IsFinish);

            PlayersResultViewModel = new PlayersResultViewModel();
            await PlayersResultViewModel.SetCoordinateAsync(Coordinate);

            await OnModeldChanged();

            //Buzzer
            await PrepareBuzzerlayoutAsync();
        }

        private async Task PrepareBuzzerlayoutAsync()
        {
            if (BuzzerControlsViewModel == null || Coordinate == null)
                return;

            var layout = Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None;

            switch (layout)
            {
                case BuzzerControlsLayout.None:
                    break;

                case BuzzerControlsLayout.Buzzer:
                    BuzzerControlsViewModel.WinnerDeclared = OnWinnerDeclared;

                    if (!Coordinate.IsDone)
                    {
                        await BuzzerControlsViewModel.ResetRoundAsync(CurrentBuzzerLayout);
                    }
                    break;

                case BuzzerControlsLayout.KeySelect:
                    await BuzzerControlsViewModel.SetKeySelectorDictionary(
                        Question?.Steps.GetKeyDictionary(),
                        Question?.BuzzerMaxAllowedKeySelect ?? 1,
                        Question?.ShowTextOnKeySelect ?? true,
                        Question?.Id);

                    break;

                case BuzzerControlsLayout.Input:
                    break;
            }
        }

        private async Task ClearBuzzerLayouts()
        {
            if (BuzzerControlsViewModel != null)
            {
                BuzzerControlsViewModel.WinnerDeclared = null;
                await BuzzerControlsViewModel.SetKeySelectorDictionary(null);

                await BuzzerControlsViewModel.ResetRoundAsync(null);
            }
        }

        public BuzzerControlsLayout CurrentBuzzerLayout => Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None;

        private async Task OnModeldChanged()
        {
            OnPropertyChanged(nameof(QuestionType));
            OnPropertyChanged(nameof(QuestionDesignation));
            OnPropertyChanged(nameof(QuestionDesignationShort));
            OnPropertyChanged(nameof(QuestionCategory));
            OnPropertyChanged(nameof(QuestionNotes));
            OnPropertyChanged(nameof(QuestionDifficulty));
            OnPropertyChanged(nameof(Phase));
            OnPropertyChanged(nameof(CurrentPoints));
            OnPropertyChanged(nameof(CurrentMinusPoints));

            OnPropertyChanged(nameof(QuestionOrderedSteps));

            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(FinishStep));
            OnPropertyChanged(nameof(NextStep));
            OnPropertyChanged(nameof(CurrentStepContext));
            OnPropertyChanged(nameof(FinishStepContext));
            OnPropertyChanged(nameof(NextStepContext));

            OnPropertyChanged(nameof(IsDone));
        }

        protected override async Task OnClosed()
        {
            GamePlayerViewModel?.SetView(null);

            await ClearBuzzerLayouts();

            await base.OnClosed();
        }

        //private Brush backgroundBrush = Brushes.DarkGray;

        //public Brush BackgroundBrush
        //{
        //    get => backgroundBrush;
        //    set
        //    {
        //        backgroundBrush = value;
        //        OnPropertyChanged();
        //    }
        //}

        //private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        //{
        //    BackgroundBrush = e switch
        //    {
        //        ServerState.None => Brushes.DarkGray,
        //        ServerState.Running => Brushes.Red,
        //        ServerState.Stopping => Brushes.Red,
        //        ServerState.Stopped => Brushes.DarkGray,
        //        ServerState.AllConnected => Brushes.Black,
        //        ServerState.ActiveState => Brushes.Black,
        //        _ => Brushes.Red
        //    };

        //    BuzzerState = e.DescriptionOrString();
        //}

        public GameGridCoordinate? Coordinate { get; set; }

        public bool IsDone
        {
            get
            {
                return Coordinate?.IsDone ?? false;
            }
            set
            {
                Coordinate?.IsDone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDoneBrush));
            }
        }

        public Brush IsDoneBrush
        {
            get
            {
                if (IsDone)
                {
                    return Brushes.DarkRed;
                }

                return Brushes.Black;
            }
        }

        public QuestionBase? Question => Coordinate?.QuestionBase;

        public BuzzerServerViewModel? BuzzerServerViewModel => StaticRessources.StaticManager.BuzzerServerViewModel;

        public override async Task VMSaveAsync()
        {
            if (Coordinate == null)
                return;

            using var ctrl = new GameGridCoordinatesController();

            await ctrl.UpdateAsync(Coordinate);
            await ctrl.SaveChangesAsync();
        }

        public string QuestionType => Question?.Typ.DescriptionOrString() ?? String.Empty;
        public string QuestionDesignation => Question?.Designation ?? String.Empty;
        public string QuestionDesignationShort => Question?.DesignationShort ?? String.Empty;
        public string QuestionCategory => Question?.Category?.Designation ?? String.Empty;
        public string QuestionNotes => Question?.Notes ?? String.Empty;
        public string QuestionDifficulty => Question?.Difficulty.DescriptionOrString().ToString() ?? String.Empty;
        public int Phase => Coordinate?.Phase ?? 0;
        public int CurrentPoints => Coordinate?.CurrentPoints ?? 0;
        public int CurrentMinusPoints => Coordinate?.CurrentMinusPoints ?? 0;

        public QuestionStepResource[] QuestionOrderedSteps => Question?.OrderedSteps ?? [];

        private QuestionStepResource? currentStep;

        public QuestionStepResource? CurrentStep
        {
            get => currentStep;
            set
            {
                currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStepContext));
            }
        }

        private QuestionStepResource? finishStep;

        public QuestionStepResource? FinishStep
        {
            get => finishStep;
            set
            {
                finishStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinishStepContext));
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
                OnPropertyChanged(nameof(NextStepContext));
            }
        }

        public QuestionStepViewContext CurrentStepContext
        {
            get
            {
                var current = new QuestionStepViewContext()
                {
                    Owner = this,
                    Step = CurrentStep,
                };

                GamePlayerViewModel?.SetView(current);
                return current;
            }
        }

        public QuestionStepViewContext FinishStepContext => new()
        {
            Owner = this,
            Step = FinishStep
        };

        public QuestionStepViewContext NextStepContext => new()
        {
            Owner = this,
            Step = NextStep
        };

        public ObservableCollection<QuestionStepResource> SelectedSteps { get; set; } = new();

        private AsyncRelayCommand? _startStepCommand;
        public AsyncRelayCommand? StartStepCommnad => _startStepCommand ??= new AsyncRelayCommand(StartStepAsync);

        private async Task StartStepAsync(object? arg)
        {
            CurrentStep = Question?.OrderedSteps.FirstOrDefault();
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
                if (CurrentStep == Question.OrderedSteps.Last())
                {
                    //MessageBox.Show("End reached");
                    return;
                }

                bool flowControl = Proceed(NextStep);
                if (!flowControl)
                {
                    return;
                }

                CurrentStep = NextStep;
                NextStep = Question.GetNextStep(CurrentStep);
            }
            else
            {
                if (CurrentStep == Question.OrderedSteps.First())
                {
                    //MessageBox.Show("Start reached");
                    return;
                }

                NextStep = CurrentStep;
                CurrentStep = Question.GetStepBehind(CurrentStep);
            }
        }

        private bool Proceed(QuestionStepResource? next)
        {
            if (Question == null || next == null)
                return true;

            if (Question.WarnOnResultStep
                && ((next?.IsResult ?? false) && (!CurrentStep?.IsResult ?? true))
                && MessageBox.Show("Next is result! Want to proceed?", "Result step ahead", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return false;
            }

            if (Question.WarnOnFinishStep
                && ((next?.IsFinish ?? false) && (!CurrentStep?.IsFinish ?? true))
                && MessageBox.Show("Next is a finish step! Want to proceed?", "Finish step ahead", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return false;
            }

            return true;
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

            bool flowControl = Proceed(first);
            if (!flowControl)
            {
                return;
            }

            CurrentStep = first;
            NextStep = Question.GetNextStep(CurrentStep);
        }

        private AsyncRelayCommand? saveIsDoneFinishStateCommand;
        public ICommand SaveIsDoneFinishStateCommand => saveIsDoneFinishStateCommand ??= new AsyncRelayCommand(SaveIsDoneFinishStateAsync);

        private async Task SaveIsDoneFinishStateAsync(object? commandParameter)
        {
            IsDone = true;

            await VMSaveAsync();

            CurrentStep = finishStep;
            NextStep = null;
        }

        #region Buzzer

        private string buzzerState = string.Empty;

        public string BuzzerState
        {
            get => buzzerState;
            set
            {
                buzzerState = value;
                OnPropertyChanged();
            }
        }

        private AsyncRelayCommand? openResultsCommand;

        public ICommand OpenResultsCommand => openResultsCommand ??= new AsyncRelayCommand(OpenResultsAsync);

        private async Task OpenResultsAsync(object? commandParameter)
        {
            await OpenResultsAsync(null, Coordinate?.Game.CurrentRound ?? 0);
        }

        private async Task OnWinnerDeclared(Player? player, int round)
        {
            await OpenResultsAsync(player, round);
        }

        private async Task OpenResultsAsync(Player? winner, int round)
        {
            PlayersResultViewModel?.CurrentBuzzerWinner = winner;

            var resultWindow = new PlayersResultView();
            resultWindow.DataContext = PlayersResultViewModel;
            resultWindow.ShowDialog();

            var newResults = PlayersResultViewModel?.Results;

            if (newResults == null || Coordinate == null || newResults.Count == 0 || Coordinate.Game.Players.Count() != newResults.Count)
                throw new Exception("Invalid result state");

            Coordinate.QuestionResults = newResults;

            if (PlayersResultViewModel?.IsDoneAndShowFinishState ?? false)
            {
                await SaveIsDoneFinishStateAsync(null);
            }
        }

        #endregion Buzzer
    }
}