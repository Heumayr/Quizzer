using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Views.GameViews.Sub
{
    public class PlayerResultContext : UcViewModelBase
    {
        private Player? currentBuzzerWinner;

        public GameGridCoordinate Coordinate { get; set; } = null!;

        public Player Player { get; set; } = null!;

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
            }
        }

        public bool IsBuzzerWinner => CurrentBuzzerWinner != null && Player.Id == CurrentBuzzerWinner.Id;
    }
}