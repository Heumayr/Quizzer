using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
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

        protected override Task OnloadAsync()
        {
            return Task.CompletedTask;
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(Player));
            OnDatagridSourceChanged();
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        public override async Task VMSaveAsync()
        {
            if (Player == null) return;

            using var ctrl = new PlayersController();
            var result = await ctrl.UpsertAsync(Player);
            await ctrl.SaveChangesAsync();

            ResultState = result.Created ? EditResultState.New : EditResultState.Updated;
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