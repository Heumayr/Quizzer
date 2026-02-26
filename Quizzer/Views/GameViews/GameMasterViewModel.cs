using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Views.GameViews
{
    public class GameMasterViewModel : ViewModelBase
    {
        private Game? game;

        public override async Task VMSaveAsync()
        {
            var gCtrl = new GenericDataHandler();
            await gCtrl.SaveToFileAsync(Loader.Games);
        }

        public Game? Game
        {
            get => game;
            set
            {
                game = value;

                OnModelChanged();
            }
        }

        private void OnModelChanged()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Players));
        }

        public QuestionBase? SelectedQuestion { get; set; }

        public List<Player> Players => Game?.Players ?? new List<Player>();
    }
}