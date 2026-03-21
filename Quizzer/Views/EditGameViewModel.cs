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
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using static Quizzer.Views.HelperViewModels.GridBuilder;

namespace Quizzer.Views
{
    public class EditGameViewModel : ViewModelBase
    {
        private ObservableCollection<Player> availablePlayers = new();
        private ObservableCollection<Player> gameAddedPlayers = new();
        private GameGridVMs gameGridVMs = new();

        public async Task LoadModel(Guid gameId)
        {
            using var ctrl = new GamesController();

            if (gameId != Guid.Empty)
            {
                var dbgame = await ctrl.GetAsync(gameId);

                if (dbgame == null)
                    throw new Exception("No Game found");

                Game = dbgame;
            }
            else
            {
                var game = new Game();

                if (string.IsNullOrEmpty(game.Designation))
                    game.Designation = "No Name - New Game";

                var inserted = await ctrl.InsertAsync(game);
                await ctrl.SaveChangesAsync();
                Game = inserted.Entity;
            }

            await OnModelChangedAsync();
        }

        private List<Player> AllPlayers { get; set; } = new();

        private ObservableCollection<Player> AvailablePlayers
        {
            get => availablePlayers;
            set
            {
                availablePlayers = value;
                OnPropertyChanged();
                AvailablePlayersView = CollectionViewSource.GetDefaultView(availablePlayers);
                OnPropertyChanged(nameof(AvailablePlayersView));
            }
        }

        public ICollectionView? AvailablePlayersView { get; private set; }

        private ObservableCollection<Player> GameAddedPlayers
        {
            get => gameAddedPlayers;
            set
            {
                gameAddedPlayers = value;
                OnPropertyChanged();
                GameAddedPlayersView = CollectionViewSource.GetDefaultView(gameAddedPlayers);
                OnPropertyChanged(nameof(GameAddedPlayersView));
            }
        }

        public ICollectionView? GameAddedPlayersView { get; private set; }

        protected override async Task OnloadAsync()
        {
            using var pCtrl = new PlayersController();
            AllPlayers = (await pCtrl.GetAllAsync()).ToList();
            CalculatePlayerLists();

            await OnModelChangedAsync();
        }

        private Game? _game;

        private Game? Game
        {
            get => _game;
            set
            {
                _game = value;
            }
        }

        public GameGridVMs GameGridVMs
        {
            get => gameGridVMs;
            private set
            {
                gameGridVMs = value;
                OnPropertyChanged();
                CellVMs = CollectionViewSource.GetDefaultView(value.CellVMs);
                ColumnHeaderVMs = CollectionViewSource.GetDefaultView(value.ColumnHeaderVMs);
                RowHeaderVMs = CollectionViewSource.GetDefaultView(value.RowHeaderVMs);
            }
        }

        public ICollectionView? CellVMs
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView? ColumnHeaderVMs
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView? RowHeaderVMs
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public async Task RebuildCellsAsync()
        {
            if (Game == null) return;

            GameGridVMs = await GridBuilder.RebuildCells(Game, CellView.Build, Game.State != GameState.Building);
        }

        public bool Restart
        {
            get => Game?.Restart ?? false;
            set
            {
                if (Game == null) return;

                Game?.Restart = value;
                OnPropertyChanged();
            }
        }

        public string Designation
        {
            get => Game?.Designation ?? "";
            set
            {
                if (Game == null) return;
                if (Game?.Designation == value) return;

                Game?.Designation = value;
                OnPropertyChanged();
            }
        }

        public double DifficultyMultiplier
        {
            get => Game?.DifficultyMultiplier ?? 0;
            set
            {
                if (Game == null) return;
                if (Game?.DifficultyMultiplier == value) return;

                Game?.DifficultyMultiplier = value;
                OnPropertyChanged();
            }
        }

        public int DifficultyAddition
        {
            get => Game?.DifficultyAddition ?? 0;
            set
            {
                if (Game == null) return;
                if (Game?.DifficultyAddition == value) return;

                Game?.DifficultyAddition = value;
                OnPropertyChanged();
            }
        }

        public double DifficultyMinusMultiplier
        {
            get => Game?.DifficultyMinusMultiplier ?? 0;
            set
            {
                if (Game == null) return;
                if (Game?.DifficultyMinusMultiplier == value) return;

                Game?.DifficultyMinusMultiplier = value;
                OnPropertyChanged();
            }
        }

        public int DifficultyMinusAddition
        {
            get => Game?.DifficultyMinusAddition ?? 0;
            set
            {
                if (Game == null) return;
                if (Game?.DifficultyMinusAddition == value) return;

                Game?.DifficultyMinusAddition = value;
                OnPropertyChanged();
            }
        }

        public double PhaseMultiplier
        {
            get => Game?.PhaseMultiplier ?? 0;
            set
            {
                if (Game == null) return;
                if (Game?.PhaseMultiplier == value) return;

                Game?.PhaseMultiplier = value;
                OnPropertyChanged();
            }
        }

        public int PhaseAddition
        {
            get => Game?.PhaseAddition ?? 0;
            set
            {
                if (Game == null) return;
                if (Game?.PhaseAddition == value) return;

                Game?.PhaseAddition = value;
                OnPropertyChanged();
            }
        }

        public int Height
        {
            get => Game?.Height ?? 0;
            set
            {
                if (Game == null) return;
                if (Game.Height == value) return;

                Game.Height = value;
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
            }
        }

        private AsyncRelayCommand? _cellClickCommand;
        public ICommand CellClickCommand => _cellClickCommand ??= new AsyncRelayCommand((p) => OnCellClickedAsync((GameGridCoordinateViewModel?)p));

        private async Task OnCellClickedAsync(GameGridCoordinateViewModel? cell)
        {
            if (cell == null || Game == null) return;

            var view = new QuestionSelectorView();

            var context = new QuestionSelectorViewModel();
            context.SetDependencys(Game, cell.Coordinate);
            view.DataContext = context;

            view.ShowDialog();

            if (cell.QuestionsId.HasValue && cell.QuestionsId != Guid.Empty)
            {
                using var ctrlQ = new QuestionBasesController();
                cell.Question = (await ctrlQ.GetAsync(cell.QuestionsId));
            }

            cell.RefreshFromModel();
        }

        public async Task OnModelChangedAsync()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Restart));
            OnPropertyChanged(nameof(Designation));
            OnPropertyChanged(nameof(IsBuilding));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(CellHeight));
            OnPropertyChanged(nameof(CellWidth));

            OnPropertyChanged(nameof(DifficultyMultiplier));
            OnPropertyChanged(nameof(DifficultyAddition));
            OnPropertyChanged(nameof(DifficultyMinusMultiplier));
            OnPropertyChanged(nameof(DifficultyMinusAddition));
            OnPropertyChanged(nameof(PhaseMultiplier));
            OnPropertyChanged(nameof(PhaseAddition));

            CalculatePlayerLists();
            OnPropertyChanged(nameof(SelectedPlayer));
            OnPropertyChanged(nameof(CmbSelectedPlayer));

            await RebuildCellsAsync();
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

            using var ctrlGames = new GamesController();
            await ctrlGames.UpsertAsync(Game);
            await ctrlGames.SaveChangesAsync();

            using var ctrlHeader = new HeadersController();
            await ctrlHeader.UpsertAsync(Game.Headers);
            await ctrlHeader.SaveChangesAsync();

            using var ctrlCells = new GameGridCoordinatesController();
            await ctrlCells.UpsertAsync(Game.GameGridCoordinates);
            await ctrlCells.SaveChangesAsync();
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }

        #region Player

        public Player? SelectedPlayer { get; set; }
        public Player? CmbSelectedPlayer { get; set; }

        //public IEnumerable<Player> AvailablePlayers => AllPlayers.Where(p => Game != null && !Game.Players.Select(p => p.Id).Contains(p.Id)).ToList();

        private AsyncRelayCommand? addPlayerCommand;
        public ICommand AddPlayerCommand => addPlayerCommand ??= new AsyncRelayCommand(AddPlayerAsync);

        private async Task AddPlayerAsync(object? commandParameter)
        {
            if (CmbSelectedPlayer == null || Game == null) return;

            var found = Game.PlayerXGames.FirstOrDefault(x => x.PlayerId == CmbSelectedPlayer.Id);

            if (found != null) return;

            using var ctrl = new PlayerXGamesController();
            var inserted = await ctrl.InsertAsync(new()
            {
                PlayerId = CmbSelectedPlayer.Id,
                GameId = Game.Id
            });

            await ctrl.SaveChangesAsync();

            inserted.Entity.Player = CmbSelectedPlayer;

            Game.PlayerXGames.Add(inserted.Entity);

            CalculatePlayerLists();
        }

        private void CalculatePlayerLists()
        {
            AvailablePlayers = new ObservableCollection<Player>(AllPlayers.Where(p => Game != null && !Game.Players.Select(p => p.Id).Contains(p.Id)).ToList());
            GameAddedPlayers = new ObservableCollection<Player>(AllPlayers.Where(p => Game != null && Game.Players.Select(p => p.Id).Contains(p.Id)).ToList());
        }

        private AsyncRelayCommand? removePlayerCommand;
        public ICommand RemovePlayerCommand => removePlayerCommand ??= new AsyncRelayCommand(RemovePlayerAsync);

        private async Task RemovePlayerAsync(object? commandParameter)
        {
            if (SelectedPlayer == null || Game == null) return;

            var found = Game.PlayerXGames.FirstOrDefault(x => x.PlayerId == SelectedPlayer.Id);

            if (found == null) return;

            using var ctrl = new PlayerXGamesController();
            await ctrl.DeleteAsync(found.Id);

            await ctrl.SaveChangesAsync();

            Game.PlayerXGames.Remove(found);

            CalculatePlayerLists();
        }

        #endregion Player

        public bool IsBuilding => Game?.State == GameState.Building;

        private AsyncRelayCommand? resetGameBuildCommand;
        public ICommand ResetGameBuildCommand => resetGameBuildCommand ??= new AsyncRelayCommand(ResetGameBuildAsync, (p) => IsBuilding);

        private async Task ResetGameBuildAsync(object? commandParameter)
        {
            if (Game == null) return;
            if (MessageBox.Show("Are you sure you want to reset the game build? This will remove all assigned questions and players from the game.", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Game.State = GameState.Building;
                using var ctrlGame = new GamesController();
                await ctrlGame.SaveChangesAsync();

                using var ctrl = new QuestionResultsController(ctrlGame);
                await ctrl.DeleteByGameIdAsync(Game.Id); //immediatly saved

                using var ctrl2 = new GameGridCoordinatesController(ctrlGame);
                await ctrl2.DeleteByGameIdAsync(Game.Id);

                using var ctrl3 = new PlayerXGamesController(ctrlGame);
                await ctrl3.DeleteByGameIdAsync(Game.Id);

                using var ctrl4 = new HeadersController(ctrlGame);
                await ctrl4.DeleteByGameIdAsync(Game.Id);

                await LoadModel(Game.Id);
            }
        }

        public int TestPhase { get; set; }

        private AsyncRelayCommand? refreshGridCommand;
        public ICommand RefreshGridCommand => refreshGridCommand ??= new AsyncRelayCommand(RefreshGridAsync);

        private async Task RefreshGridAsync(object? commandParameter)
        {
            if (Game == null) return;

            foreach (var cell in Game.GameGridCoordinates)
            {
                cell.Phase = TestPhase;
            }

            await RebuildCellsAsync();
        }

        public static async Task ResetGameResultsAsync(Game game)
        {
            if (game == null) return;

            var mbResult = MessageBox.Show($"Delete all results for this Game: {game.Designation}", "Deletion", MessageBoxButton.YesNo);

            if (mbResult != MessageBoxResult.Yes) return;

            using var ctrl = new QuestionResultsController();
            await ctrl.DeleteByGameIdAsync(game.Id);

            using var ctrlCoords = new GameGridCoordinatesController(ctrl);
            await ctrlCoords.UpdateIsDoneStateOfGame(game.Id, false);
            await ctrlCoords.UpdatePhaseOfGame(game.Id, 1);

            game.CalculateAndSetCurrentPoints();
        }

        private AsyncRelayCommand? setToBuildingModeCommand;

        public ICommand SetToBuildingModeCommand => setToBuildingModeCommand ??= new AsyncRelayCommand(SetToBuildingModeAsync);

        private async Task SetToBuildingModeAsync(object? commandParameter)
        {
            if (Game == null) return;

            Game.State = GameState.Building;
            await VMSaveAsync();
            await LoadModel(Game.Id);
        }
    }
}