using Quizzer.Base;

using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class GamesViewModel : ViewModelBase
    {
        public ICollectionView? GamesView { get; set; }

        public ObservableCollection<Game> Games
        {
            get => games;
            set
            {
                games = value;
                OnPropertyChanged();
                GamesView = CollectionViewSource.GetDefaultView(games);
                OnPropertyChanged(nameof(GamesView));
            }
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }

        //private AsyncRelayCommand? saveCommand;
        //public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        //private async Task SaveCommandAsync(object? param)
        //{
        //    await VMSaveAsync();
        //}

        protected override async Task OnloadAsync()
        {
            using var ctrl = new GamesController();
            Games = new ObservableCollection<Game>(await ctrl.GetAllAsync());
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
                    await vm.LoadModel(game.Id);
                    this.Window?.Hide();
                    window.Closed += OnClosed;
                    window.ShowDialog();

                    await OnloadAsync();
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
        private ObservableCollection<Game> games = new();

        public ICommand RemoveGameCommand => removeGameCommand ??= new AsyncRelayCommand(RemoveGameAsync);

        private async Task RemoveGameAsync(object? commandParameter)
        {
            if (SelectedGames == null || SelectedGames.Count == 0)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show("Are you sure you want to remove the selected game(s)?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var toRemove = new List<Game>(SelectedGames);
            using var ctrl = new GamesController();
            foreach (var game in toRemove)
            {
                await ctrl.DeleteAsync(game.Id);
            }

            await ctrl.SaveChangesAsync();
            await OnloadAsync();
        }

        private AsyncRelayCommand? startGameCommand;
        public ICommand StartGameCommand => startGameCommand ??= new AsyncRelayCommand(StartGameAsync);

        private async Task StartGameAsync(object? commandParameter)
        {
            var game = SelectedGames.FirstOrDefault();

            if (game == null || game.Id == Guid.Empty) return;

            try
            {
                this.Window?.Hide();

                var gameView = new GameMasterView();
                var gameContext = new GameMasterViewModel();
                gameView.DataContext = gameContext;

                var loadedGame = await gameContext.LoadModel(game.Id);

                if (loadedGame == null)
                {
                    this.Window?.Show();
                    return;
                }

                gameView.Closed += async (_, __) =>
                {
                    this.Window?.Show();
                };

                gameView.Show();
            }
            catch (Exception)
            {
                this.Window?.Show();
                throw;
            }
            finally
            {
            }
        }
    }
}