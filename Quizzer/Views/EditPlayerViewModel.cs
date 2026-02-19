using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class EditPlayerViewModel : ViewModelBase
    {
        public void SetPlayer(Player player)
        {
            Player = player;
        }

        public EditResultState ResultState { get; set; } = EditResultState.Cancelled;

        private Player? _player;

        public Player? Player
        {
            get => _player;
            set
            {
                if (!Equals(_player, value))
                {
                    _player = value;
                    OnModelChanged();
                }
            }
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(Player));
            OnDatagridSourceChanged();
        }

        private AsyncRelayCommand? saveCommand;
        private QuestionResult? selectedQuestionResut;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        public override async Task VMSaveAsync()
        {
            if (Player == null)
            {
                return;
            }

            if (Player.Id == Guid.Empty)
            {
                Player.Id = Guid.NewGuid();
                ResultState = EditResultState.New;
                Loader.Players.Add(Player);
            }
            else
            {
                ResultState = EditResultState.Updated;
            }

            var ctrl = new GenericDataHandler();

            await ctrl.SaveToFileAsync(Loader.Players);
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }

        private void OnDatagridSourceChanged()
        {
            if (Player == null)
            {
                return;
            }

            var view = CollectionViewSource.GetDefaultView(Player.CurrentQuestionResults);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(
                new System.ComponentModel.SortDescription(
                    nameof(QuestionResult.Game.Designation),
                    System.ComponentModel.ListSortDirection.Ascending));

            view.Refresh();
        }
    }
}