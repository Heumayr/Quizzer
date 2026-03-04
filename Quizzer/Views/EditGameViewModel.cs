using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.ViewModels;
using Quizzer.Views.Base;
using Quizzer.Views.GameViews;
using Quizzer.Views.HelperViewModels;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Quizzer.Views
{
    public class EditGameViewModel : ViewModelBase
    {
        public async Task SetGame(Game game)
        {
            if (game.Id == Guid.Empty)
                throw new Exception("No Game Id");

            using var ctrl = new GamesController();
            var dbgame = await ctrl.GetAsync(game.Id);

            if (dbgame == null)
                throw new Exception("No Game found");

            Game = dbgame;
        }

        private List<Player> AllPlayers { get; set; } = new();

        protected override async Task Onload()
        {
            using var pCtrl = new PlayersController();
            AllPlayers = (await pCtrl.GetAllAsync()).ToList();
        }

        public EditResultState ResultState { get; set; } = EditResultState.Cancelled;

        private Game? _game;

        internal Game? Game
        {
            get => _game;
            set
            {
                if (!Equals(_game, value))
                {
                    _game = value;
                    OnModelChanged();
                }
            }
        }

        public ObservableCollection<GameGridCoordinateViewModel> CellVMs { get; } = new();
        public ObservableCollection<HeaderEntryViewModel> ColumnHeaderVMs { get; } = new();
        public ObservableCollection<HeaderEntryViewModel> RowHeaderVMs { get; } = new();

        public void RebuildCells()
        {
            if (Game == null) return;

            GridBuilder.RebuildCells(Game, CellVMs, ColumnHeaderVMs, RowHeaderVMs, CellView.Build);
        }

        public int Height
        {
            get => Game?.Height ?? 0;
            set
            {
                if (Game == null) return;
                if (Game.Height == value) return;

                Game.Height = value;
                OnPropertyChanged(nameof(Height));
                RebuildCells();
            }
        }

        public int Width
        {
            get => Game?.Width ?? 0;
            set
            {
                if (Game == null) return;
                if (Game.Width == value) return;

                Game.Width = value;
                OnPropertyChanged(nameof(Width));
                RebuildCells();
            }
        }

        public double CellHeight
        {
            get => Game?.CellHeight ?? 60;
            set
            {
                if (Game == null) return;
                if (Game.CellHeight == value) return;

                Game.CellHeight = value;
                OnPropertyChanged(nameof(CellHeight));
            }
        }

        public double CellWidth
        {
            get => Game?.CellWidth ?? 120;
            set
            {
                if (Game == null) return;
                if (Game.CellWidth == value) return;

                Game.CellWidth = value;
                OnPropertyChanged(nameof(CellWidth));
            }
        }

        private ICommand? _cellClickCommand;
        public ICommand CellClickCommand => _cellClickCommand ??= new RelayCommand<GameGridCoordinateViewModel>(OnCellClicked);

        private void OnCellClicked(GameGridCoordinateViewModel? cell)
        {
            if (cell == null || Game == null) return;

            var view = new QuestionSelectorView();

            var context = new QuestionSelectorViewModel();
            context.SetDependencys(Game, cell.Coordinate);
            view.DataContext = context;

            view.ShowDialog();

            cell.RefreshFromModel();
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(CellHeight));
            OnPropertyChanged(nameof(CellWidth));

            OnPropertyChanged(nameof(AvailablePlayers));
            OnPropertyChanged(nameof(SelectedPlayer));
            OnPropertyChanged(nameof(CmbSelectedPlayer));
            OnPropertyChanged(nameof(IsBuilding));
            OnPlayerListSourceChanged();
            RebuildCells();
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        public override async Task VMSaveAsync()
        {
            if (Game == null) return;

            using var ctrl = new GamesController();
            var result = await ctrl.UpsertAsync(Game);
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

        #region Player

        private void OnPlayerListSourceChanged()
        {
            if (Game == null)
            {
                return;
            }

            var view = CollectionViewSource.GetDefaultView(Game.Players);
            view.Refresh();

            OnPropertyChanged(nameof(AvailablePlayers));
            //var viewCmb = CollectionViewSource.GetDefaultView(AvailablePlayers);
            //viewCmb.Refresh();
        }

        public Player? SelectedPlayer { get; set; }
        public Player? CmbSelectedPlayer { get; set; }

        public IEnumerable<Player> AvailablePlayers => AllPlayers.Where(p => Game != null && !Game.Players.Select(p => p.Id).Contains(p.Id)).ToList();

        private AsyncRelayCommand? addPlayerCommand;
        public ICommand AddPlayerCommand => addPlayerCommand ??= new AsyncRelayCommand(AddPlayerAsync);

        private async Task AddPlayerAsync(object? commandParameter)
        {
            if (CmbSelectedPlayer == null || Game == null) return;

            var found = Game.PlayerXGames.FirstOrDefault(x => x.PlayerId == CmbSelectedPlayer.Id);

            if (found == null) return;

            using var ctrl = new PlayerXGamesController();
            var inserted = await ctrl.InsertAsync(new()
            {
                PlayerId = CmbSelectedPlayer.Id,
                GamesId = Game.Id
            });

            await ctrl.SaveChangesAsync();

            Game.PlayerXGames.Add(inserted.Entity);

            OnPlayerListSourceChanged();
        }

        private AsyncRelayCommand? removePlayerCommand;
        public ICommand RemovePlayerCommand => removePlayerCommand ??= new AsyncRelayCommand(RemovePlayerAsync);

        private async Task RemovePlayerAsync(object? commandParameter)
        {
            if (SelectedPlayer == null || Game == null) return;

            var found = Game.PlayerXGames.FirstOrDefault(x => x.PlayerId == CmbSelectedPlayer.Id);

            if (found == null) return;

            using var ctrl = new PlayerXGamesController();
            await ctrl.DeleteAsync(found.Id);

            await ctrl.SaveChangesAsync();

            Game.PlayerXGames.Remove(found);

            OnPlayerListSourceChanged();
        }

        #endregion Player

        public bool IsBuilding => Game?.State == GameState.Building;

        private RelayCommand? resetGameBuildCommand;
        public ICommand ResetGameBuildCommand => resetGameBuildCommand ??= new RelayCommand(ResetGameBuild, (p) => IsBuilding);

        private void ResetGameBuild(object? commandParameter)
        {
            if (Game == null) return;
            if (MessageBox.Show("Are you sure you want to reset the game build? This will remove all assigned questions and players from the game.", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Game.State = GameState.Building;
                Game.GameGridCoordinates.Clear();
                Game.PlayerXGames.Clear();
                Game.ColumnHeader.Clear();
                Game.RowHeader.Clear();
                Game.QuestionResults.Clear();
                OnModelChanged();
            }
        }

        public int TestPhase { get; set; }

        private RelayCommand? refreshGridCommand;
        public ICommand RefreshGridCommand => refreshGridCommand ??= new RelayCommand(RefreshGrid);

        private void RefreshGrid(object? commandParameter)
        {
            if (Game == null) return;

            foreach (var cell in Game.GameGridCoordinates)
            {
                cell.Phase = TestPhase;
            }

            RebuildCells();
        }

        private AsyncRelayCommand? startGameCommand;
        public ICommand StartGameCommand => startGameCommand ??= new AsyncRelayCommand(StartGameAsync);

        private async Task StartGameAsync(object? commandParameter)
        {
            if (Game == null) return;

            if (Game.Players.Count() == 0)
            {
                MessageBox.Show("Cannot start the game without any players. Please add at least one player before starting.", "No Players", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Game.GameGridCoordinates.Count == 0)
            {
                MessageBox.Show("Cannot start the game without any questions assigned. Please assign at least one question to the grid before starting.", "No Questions", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Game.State != GameState.Finished)
                Game.State = GameState.InProgress;

            if (Game.Restart)
            {
                Game.Restart = false;
                ResetGameResults();
            }

            OnModelChanged();
            await VMSaveAsync();

            try
            {
                this.Window?.Hide();

                var gameView = new GameMasterView();
                var gameContext = new GameMasterViewModel();
                gameView.DataContext = gameContext;

                gameContext.Game = Game;
                StaticManager.BuzzerServerViewModel.Game = Game;

                gameView.Closed += (_, __) => this.Window?.Show();
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

        public void ResetGameResults()
        {
            if (Game == null) return;

            Game.QuestionResults.Clear();

            foreach (var cell in Game.GameGridCoordinates)
            {
                cell.Phase = 1;
                cell.IsDone = false;
                cell.CalculatedPoints();
            }

            RebuildCells();
        }

        private RelayCommand? setToBuildingModeCommand;
        public ICommand SetToBuildingModeCommand => setToBuildingModeCommand ??= new RelayCommand(SetToBuildingMode);

        private void SetToBuildingMode(object? commandParameter)
        {
            Game?.State = GameState.Building;
            OnPropertyChanged(nameof(IsBuilding));
        }
    }
}