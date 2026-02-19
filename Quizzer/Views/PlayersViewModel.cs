using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
using Quizzer.Datamodels.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class PlayersViewModel : ViewModelBase
    {
        public ObservableCollection<Player> Players { get => Loader.Players; set => Loader.Players = value; }

        public override Task VMSaveAsync()
        {
            var ctrl = new GenericDataHandler();
            return ctrl.SaveToFileAsync(Players);
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        private async Task SaveCommandAsync(object? param)
        {
            await VMSaveAsync();
        }

        public ObservableCollection<Player> SelectedPlayers { get; set; } = new();

        private AsyncRelayCommand? openPlayerCommand;
        public ICommand OpenPlayerCommand => openPlayerCommand ??= new AsyncRelayCommand(OpenPlayerAsync);

        private async Task OpenPlayerAsync(object? model)
        {
            if (model is Player player)
            {
                await EditPlayerAsync(player);
            }
        }

        private AsyncRelayCommand? addPlayerCommand;
        public ICommand AddPlayerCommand => addPlayerCommand ??= new AsyncRelayCommand((p) => EditPlayerAsync(new Player()));

        private async Task EditPlayerAsync(Player player)
        {
            var window = new EditPlayerView();

            if (window.DataContext is EditPlayerViewModel vm)
            {
                vm.SetPlayer(player);
                window.ShowDialog();

                if (vm.ResultState == EditResultState.New || vm.ResultState == EditResultState.Updated || vm.ResultState == EditResultState.Deleted)
                {
                    OnDatagridSourceChanged();
                }
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        public void OnDatagridSourceChanged()
        {
            CollectionViewSource.GetDefaultView(Players)?.Refresh();
        }

        private AsyncRelayCommand? removePlayerCommand;
        public ICommand RemovePlayerCommand => removePlayerCommand ??= new AsyncRelayCommand(RemovePlayerAsync);

        private async Task RemovePlayerAsync(object? commandParameter)
        {
            MessageBoxResult result = MessageBox.Show("Are you sure you want to remove the selected player(s)?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            if (SelectedPlayers == null || SelectedPlayers.Count == 0)
            {
                return;
            }

            var toRemove = new List<Player>(SelectedPlayers);

            foreach (var player in toRemove)
            {
                Players.Remove(player);
            }

            await VMSaveAsync();
            OnDatagridSourceChanged();

            return;
        }
    }
}