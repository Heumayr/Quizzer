using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.Views.GameViews.Sub
{
    public class PlayerStatsContext : UcViewModelBase
    {
        private Player player = null!;
        private Game game = null!;
        private int placement;

        public GameMasterViewModel? GameMasterViewModel { get; set; }
        public bool IsGameFinished => GameMasterViewModel?.IsGameFinished ?? false;

        public int Placement
        {
            get => placement;
            set
            {
                placement = value;
                OnPropertyChanged(nameof(Placement));
                OnPropertyChanged(nameof(PlacementText));
                OnPropertyChanged(nameof(IsWinner));
                OnPropertyChanged(nameof(ScaleFactor));
                OnPropertyChanged(nameof(CardWidth));
                OnPropertyChanged(nameof(CardHeight));
                OnPropertyChanged(nameof(DesignationBrush));
            }
        }

        public string PlacementText => Placement == 0 ? string.Empty : $"{Placement}.";

        public bool IsWinner => Placement == 1;

        public Brush DesignationBrush => Placement switch
        {
            1 => IsGameFinished ? Brushes.Black : (Brush)new BrushConverter().ConvertFrom("#C8A96B")!,
            2 => (Brush)new BrushConverter().ConvertFrom("#C8A96B")!,
            3 => (Brush)new BrushConverter().ConvertFrom("#C8A96B")!,
            4 => (Brush)new BrushConverter().ConvertFrom("#C8A96B")!,
            _ => (Brush)new BrushConverter().ConvertFrom("#C8A96B")!
        };

        public double ScaleFactor => Placement switch
        {
            1 => 1.0,
            2 => 0.88,
            3 => 0.82,
            4 => 0.76,
            _ => 0.70
        };

        public double CardWidth => Placement switch
        {
            1 => 260,
            2 => 190,
            3 => 175,
            4 => 165,
            _ => 155
        };

        public double CardHeight => Placement switch
        {
            1 => 420,
            2 => 300,
            3 => 280,
            4 => 265,
            _ => 250
        };

        public StatsContext StatsContext { get; set; } = null!;

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

        public Game Game
        {
            get => game;
            set
            {
                game = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualScore));
            }
        }

        public string PlayerImagePath => string.IsNullOrEmpty(Player?.UserPictureFileName) ? Settings.PlaceholderPlayerImagePath : Path.Combine(Settings.FilePathQuizzer, Player.UserPictureFileName);

        public Brush BackgroundBrush => GetBackgroundBrush();

        private Brush GetBackgroundBrush()
        {
            if (StatsContext.Winners.Select(p => p.Id).Contains(Player.Id))
                return StaticRessources.StaticResources.PlayerCardWinnerImageBrush;

            return StaticRessources.StaticResources.PlayerCardImageBrush;
        }

        public int ActualScore => CalculateScore();

        private int CalculateScore()
        {
            if (Player == null || Game == null) return 0;

            var results = Game.QuestionResults.Where(x => x.PlayerId == Player.Id);

            var score = results.Sum(r => r.FinalScore);

            return score;
        }

        public void UpdateScore()
        {
            OnPropertyChanged(nameof(ActualScore));
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }
}