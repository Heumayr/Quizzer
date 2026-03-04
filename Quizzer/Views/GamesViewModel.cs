using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class GamesViewModel : ViewModelBase
    {
        public ObservableCollection<Game> Games { get => Loader.Games; set => Loader.Games = value; }

        public override Task VMSaveAsync()
        {
            var ctrl = new GenericDataHandler();
            return ctrl.SaveToFileAsync(Games);
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        private async Task SaveCommandAsync(object? param)
        {
            await VMSaveAsync();
        }

        public ObservableCollection<Game> SelectedGames { get; set; } = new();

        private AsyncRelayCommand? openGameCommand;
        public ICommand OpenGameCommand => openGameCommand ??= new AsyncRelayCommand(OpenGameAsync);

        private async Task OpenGameAsync(object? model)
        {
            if (model is Game game)
            {
                await EditGameAsync(game);
            }
        }

        private AsyncRelayCommand? addGameCommand;
        public ICommand AddGameCommand => addGameCommand ??= new AsyncRelayCommand((p) => EditGameAsync(new Game()));

        private async Task EditGameAsync(Game game)
        {
            try
            {
                var window = new EditGameView();

                if (window.DataContext is EditGameViewModel vm)
                {
                    vm.SetGame(game);
                    this.Window?.Hide();
                    window.Closed += OnClosed;
                    window.ShowDialog();

                    if (vm.ResultState == EditResultState.New || vm.ResultState == EditResultState.Updated || vm.ResultState == EditResultState.Deleted)
                    {
                        OnDatagridSourceChanged();
                    }
                }
                else
                {
                    throw new InvalidOperationException("DataContext is not of type EditGameViewModel");
                }
            }
            catch (Exception)
            {
                this.Window?.Show();
                addGameCommand?.RaiseCanExecuteChanged();
                openGameCommand?.RaiseCanExecuteChanged();
                throw;
            }
            finally
            {
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            this.Window?.Show();
            addGameCommand?.RaiseCanExecuteChanged();
            openGameCommand?.RaiseCanExecuteChanged();
        }

        public void OnDatagridSourceChanged()
        {
            CollectionViewSource.GetDefaultView(Games)?.Refresh();
        }

        private AsyncRelayCommand? removeGameCommand;
        public ICommand RemoveGameCommand => removeGameCommand ??= new AsyncRelayCommand(RemoveGameAsync);

        private async Task RemoveGameAsync(object? commandParameter)
        {
            MessageBoxResult result = MessageBox.Show("Are you sure you want to remove the selected game(s)?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            if (SelectedGames == null || SelectedGames.Count == 0)
            {
                return;
            }

            var toRemove = new List<Game>(SelectedGames);

            foreach (var game in toRemove)
            {
                Games.Remove(game);
            }

            await VMSaveAsync();
            OnDatagridSourceChanged();

            return;
        }
    }
}