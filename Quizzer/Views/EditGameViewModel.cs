using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
using Quizzer.ViewModels;
using Quizzer.Views.HelperViewModels;
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
        public void SetGame(Game game)
        {
            Game = game;
        }

        public EditResultState ResultState { get; set; } = EditResultState.Cancelled;

        private Game? _game;

        public Game? Game
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

        private void RebuildCells()
        {
            if (Game == null) return;

            int h = Game.Height;
            int w = Game.Width;

            CellVMs.Clear();
            ColumnHeaderVMs.Clear();
            RowHeaderVMs.Clear();

            if (h <= 0 || w <= 0)
            {
                //clear model too when invalid
                Game.GameGridCoordinates.Clear();
                Game.ColumnHeader.Clear();
                Game.RowHeader.Clear();
                return;
            }

            // 1) Remove out-of-bounds coordinates from MODEL
            for (int i = Game.GameGridCoordinates.Count - 1; i >= 0; i--)
            {
                var c = Game.GameGridCoordinates[i];
                if (c.X < 0 || c.X >= w || c.Y < 0 || c.Y >= h)
                    Game.GameGridCoordinates.RemoveAt(i);
            }

            // 2) Remove out-of-bounds headers from MODEL
            foreach (var key in Game.ColumnHeader.Keys.ToList())
                if (key < 0 || key >= w)
                    Game.ColumnHeader.Remove(key);

            foreach (var key in Game.RowHeader.Keys.ToList())
                if (key < 0 || key >= h)
                    Game.RowHeader.Remove(key);

            // 3) Ensure header entries exist for all indices
            for (int x = 0; x < w; x++)
                if (!Game.ColumnHeader.ContainsKey(x))
                    Game.ColumnHeader[x] = (x + 1).ToString();

            for (int y = 0; y < h; y++)
                if (!Game.RowHeader.ContainsKey(y))
                    Game.RowHeader[y] = (y + 1).ToString();

            // 4) Build header VMs (directly read/write dictionaries)
            for (int x = 0; x < w; x++)
                ColumnHeaderVMs.Add(new HeaderEntryViewModel(Game.ColumnHeader, x, isColumnHeader: true));

            for (int y = 0; y < h; y++)
                RowHeaderVMs.Add(new HeaderEntryViewModel(Game.RowHeader, y, isColumnHeader: false));

            // 5) Map remaining coords and ensure full matrix exists
            var map = Game.GameGridCoordinates
                .GroupBy(c => (c.Y, c.X))
                .ToDictionary(g => g.Key, g => g.First());

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!map.TryGetValue((y, x), out var cell))
                    {
                        cell = new GameGridCoordinate(y, x);
                        Game.GameGridCoordinates.Add(cell);
                        map[(y, x)] = cell;
                    }

                    var cellVM = new GameGridCoordinateViewModel(cell);
                    cell.Game = Game; // set reference for easier access in VM
                    cell.CalculatedPoints();
                    CellVMs.Add(cellVM);
                }
            }
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
            if (Game == null)
            {
                return;
            }

            if (Game.Id == Guid.Empty)
            {
                Game.Id = Guid.NewGuid();
                ResultState = EditResultState.New;
                Loader.Games.Add(Game);
            }
            else
            {
                ResultState = EditResultState.Updated;
            }

            var ctrl = new GenericDataHandler();

            await ctrl.SaveToFileAsync(Loader.Games);
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

        public IEnumerable<Player> AvailablePlayers => Loader.Players.Where(p => Game != null && !Game.PlayerIds.Contains(p.Id)).ToList();

        private RelayCommand? addPlayerCommand;
        public ICommand AddPlayerCommand => addPlayerCommand ??= new RelayCommand(AddPlayer);

        private void AddPlayer(object? commandParameter)
        {
            if (CmbSelectedPlayer == null || Game == null) return;

            Game.Players.Add(CmbSelectedPlayer);
            if (!Game.PlayerIds.Contains(CmbSelectedPlayer.Id))
                Game.PlayerIds.Add(CmbSelectedPlayer.Id);

            OnPlayerListSourceChanged();
        }

        private RelayCommand? removePlayerCommand;
        public ICommand RemovePlayerCommand => removePlayerCommand ??= new RelayCommand(RemovePlayer);

        private void RemovePlayer(object? commandParameter)
        {
            if (SelectedPlayer == null || Game == null) return;

            Game.Players.Remove(SelectedPlayer);

            var id = Game.PlayerIds.FirstOrDefault(pid => pid == SelectedPlayer.Id);
            if (id != Guid.Empty)
                Game.PlayerIds.Remove(id);

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
                Game.Players.Clear();
                Game.PlayerIds.Clear();
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

            if (Game.Players.Count == 0)
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