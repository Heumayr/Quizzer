using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.Sub
{
    public class PlayerResultContext : UcViewModelBase
    {
        private Player? currentBuzzerWinner;
        private Player player = null!;

        public GameGridCoordinate Coordinate { get; set; } = null!;

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

        public QuestionResult Result { get; set; } = null!;

        public QuestionBase? Question => Coordinate?.QuestionBase;

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

        public bool IsBuzzerWinner => CurrentBuzzerWinner != null && Player.Id == CurrentBuzzerWinner.Id;

        public string PlayerImagePath => string.IsNullOrEmpty(Player?.UserPictureFileName) ? Settings.PlaceholderPlayerImagePath : Path.Combine(Settings.FilePathQuizzer, Player.UserPictureFileName);

        public Brush BackgroundBrush => GetBackgroundBrush();

        private Brush GetBackgroundBrush()
        {
            if (IsBuzzerWinner)
                return Brushes.DarkGreen;

            return Brushes.Black;
        }
    }
}