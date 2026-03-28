using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.Views.GameViews.Sub
{
    public class PlayerResultContext : UcViewModelBase
    {
        public enum ScoreSuggestion
        {
            None,
            Right,
            Wrong,
            Correction
        }

        private Player? currentBuzzerWinner;
        private Player player = null!;
        private ScoreSuggestion suggestion;
        private QuestionResult result = null!;

        public PlayersResultViewModel PlayersResultViewModel { get; set; } = null!;

        public GameGridCoordinate? Coordinate => PlayersResultViewModel?.Coordinate;

        public Player Player
        {
            get => player;
            set
            {
                player = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayerImagePath));
            }
        }

        public QuestionResult Result
        {
            get => result;
            set
            {
                result = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualScore));
            }
        }

        public SelectionResult? SelectionResult
        {
            get => field;
            set
            {
                if (value != null && value.PlayerId != Player.Id)
                    throw new Exception("Invalid player for selection result");

                field = value;
                OnPropertyChanged();
                CompareResults(value?.SelectedKeys);
            }
        }

        private void CompareResults(List<string>? selectedKeys)
        {
            var keyViews = new List<KeyView>();
            var question = PlayersResultViewModel?.Coordinate?.QuestionBase;

            if (selectedKeys == null || question == null || selectedKeys.Count == 0)
            {
                KeyViews = keyViews;
                ShowKeySelections = Visibility.Collapsed;
                OnPropertyChanged(nameof(KeyViews));
                OnPropertyChanged(nameof(ShowKeySelections));
                return;
            }

            foreach (var key in selectedKeys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                var step = question.Steps.FirstOrDefault(s => s.QuestionViewKey == key);

                var keyView = new KeyView
                {
                    Key = key,
                    Designation = step?.Designation ?? step?.StepText,
                    IsCorrect = step != null && step.IsResult,
                    ShowDesignation = true
                };

                keyViews.Add(keyView);
            }

            KeyViews = keyViews;

            //alle richtig
            if (keyViews.All(k => k.IsCorrect) && question.BuzzerMaxAllowedKeySelect == keyViews.Count)
            {
                Suggestion = ScoreSuggestion.Right;
            }
            else
            {
                Suggestion = ScoreSuggestion.None;
            }

            ShowKeySelections = Visibility.Visible;
            OnPropertyChanged(nameof(KeyViews));
            OnPropertyChanged(nameof(ShowKeySelections));
        }

        public class KeyView
        {
            public string Key { get; set; } = string.Empty;

            public string? Designation { get; set; }

            public bool IsCorrect { get; set; }

            public bool ShowDesignation { get; set; } = false;

            public Visibility ShowDesignationVisibility => ShowDesignation ? Visibility.Visible : Visibility.Collapsed;
        }

        public List<KeyView> KeyViews { get; set; } = new();

        public Player? CurrentBuzzerWinner
        {
            get => currentBuzzerWinner;
            set
            {
                currentBuzzerWinner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBuzzerWinner));
                OnPropertyChanged(nameof(BackgroundBrush));
            }
        }

        public Visibility ShowKeySelections { get; set; } = Visibility.Collapsed;

        public bool IsBuzzerWinner => CurrentBuzzerWinner != null && Player.Id == CurrentBuzzerWinner.Id;

        public string PlayerImagePath => string.IsNullOrEmpty(Player?.UserPictureFileName) ? Settings.PlaceholderPlayerImagePath : Path.Combine(Settings.FilePathQuizzer, Player.UserPictureFileName);

        public Brush BackgroundBrush => GetBackgroundBrush();

        private Brush GetBackgroundBrush()
        {
            if (IsBuzzerWinner)
                return Brushes.DarkGreen;

            return Brushes.Black;
        }

        public ScoreSuggestion Suggestion
        {
            get => suggestion;
            set
            {
                suggestion = value;
                OnPropertyChanged();

                CurrentScoreManipulation = suggestion switch
                {
                    ScoreSuggestion.Right => Coordinate?.CurrentPoints ?? 0,
                    ScoreSuggestion.Wrong => Coordinate?.CurrentMinusPoints * -1 ?? 0,
                    _ => 0,
                };

                if (value == ScoreSuggestion.Right)
                    CorrectAnswered = true;
                else if (value == ScoreSuggestion.Wrong)
                    CorrectAnswered = false;

                OnPropertyChanged(nameof(CanNotEditManipulation));
            }
        }

        public bool CanNotEditManipulation => Suggestion != ScoreSuggestion.Correction;

        public void SetManipulationToResult()
        {
            switch (Suggestion)
            {
                case ScoreSuggestion.Right:
                    if (CurrentScoreManipulation < 0) throw new Exception("Invalid positiv score");
                    Result.Score += CurrentScoreManipulation;
                    Result.RightCount++;
                    break;

                case ScoreSuggestion.Wrong:
                    if (CurrentScoreManipulation > 0) throw new Exception("Invalid negativ score");
                    Result.WrongCount++;
                    Result.MinusScore += CurrentScoreManipulation * -1;
                    break;

                case ScoreSuggestion.Correction:
                    Result.CorrectionsCount++;
                    Result.Correction += CurrentScoreManipulation;
                    break;
            }

            Suggestion = ScoreSuggestion.None;
            OnPropertyChanged(nameof(ActualScore));
        }

        public void RefreshUIOnModelSave()
        {
            OnPropertyChanged(nameof(Result));
            OnPropertyChanged(nameof(CorrectAnswered));
            OnPropertyChanged(nameof(ActualScore));
            OnPropertyChanged(nameof(CurrentScoreManipulation));
            OnPropertyChanged(nameof(NewScore));
        }

        public int CurrentScoreManipulation
        {
            get
            {
                return field;
            }
            set
            {
                field = value;
                OnPropertyChanged();

                NewScore = Result.FinalScore + value;
            }
        }

        public int NewScore
        {
            get
            {
                return field;
            }
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public bool CorrectAnswered
        {
            get
            {
                return Result.CorrectAnswered;
            }
            set
            {
                Result.CorrectAnswered = value;
                OnPropertyChanged();
            }
        }

        public int ActualScore
        {
            get
            {
                return Result?.FinalScore ?? 0;
            }
        }

        private RelayCommand? rightSuggestionCommand;
        public ICommand RightSuggestionCommand => rightSuggestionCommand ??= new RelayCommand(RightSuggestion);

        private void RightSuggestion(object? commandParameter)
        {
            Suggestion = ScoreSuggestion.Right;
        }

        private RelayCommand? wrongSuggestionCommand;
        public ICommand WrongSuggestionCommand => wrongSuggestionCommand ??= new RelayCommand(WrongSuggestion);

        private void WrongSuggestion(object? commandParameter)
        {
            Suggestion = ScoreSuggestion.Wrong;
        }

        private RelayCommand? noneSuggestionCommand;
        public ICommand NoneSuggestionCommand => noneSuggestionCommand ??= new RelayCommand(NoneSuggestion);

        private void NoneSuggestion(object? commandParameter)
        {
            Suggestion = ScoreSuggestion.None;
        }

        private RelayCommand? correctionSuggestionCommand;
        public ICommand CorrectionSuggestionCommand => correctionSuggestionCommand ??= new RelayCommand(CorrectionSuggestion);

        private void CorrectionSuggestion(object? commandParameter)
        {
            Suggestion = ScoreSuggestion.Correction;
        }

        //public int Score
        //{
        //    get
        //    {
        //        return Result.Score;
        //    }
        //    set
        //    {
        //        Result.Score = value;
        //        OnPropertyChanged();
        //    }
        //}

        //public int MinusScore
        //{
        //    get
        //    {
        //        return Result.MinusScore;
        //    }
        //    set
        //    {
        //        Result.MinusScore = value;
        //        OnPropertyChanged();
        //    }
        //}
    }
}