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

        public int Placement
        {
            get => placement;
            set
            {
                placement = value;
                OnPropertyChanged(nameof(Placement));
            }
        }

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