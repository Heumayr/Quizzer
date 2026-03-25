using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.ViewModels;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.Sub;
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
using System.Windows.Media;
using static Quizzer.Views.HelperViewModels.GridBuilder;

namespace Quizzer.Views.GameViews
{
    public class GameMasterViewModel : ViewModelBase
    {
        private List<Window> OpenGamePlayerViews = new();

        public GamePlayerViewModel GamePlayerViewModel { get; private set; } = new();

        public StatsContext StatsContext { get; private set; } = new();

        private BuzzerServerView? buzzerServerView = null;

        private string backgroundImagePath = Settings.BackgroundImagePath;
        public Brush HeaderColumnBrush { get; set; } = StaticResources.HeaderColumnImageBrush;
        public Brush HeaderRowBrush { get; set; } = StaticResources.HeaderRowImageBrush;

        public string BackgroundImagePath
        {
            get => backgroundImagePath;
            set
            {
                backgroundImagePath = value;
                OnPropertyChanged();
            }
        }

        public GameMasterViewModel()
        {
            //StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        protected override async Task OnloadAsync()
        {
            await OnModelChangedAsync();
            GamePlayerViewModel.Game = Game;
        }

        protected override Task OnClosed()
        {
            try
            {
                foreach (var gpv in OpenGamePlayerViews)
                {
                    try
                    {
                        gpv?.Close();
                    }
                    catch
                    {
                    }
                }

                buzzerServerView?.Close();

                if (StaticManager.BuzzerServerViewModel != null && StaticManager.BuzzerServerViewModel.IsBuzzerServerRunning)
                {
                    StaticManager.BuzzerServerViewModel.StopServerCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }

            return base.OnClosed();
        }

        //private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        //{
        //    BackgroundBrush = e switch
        //    {
        //        ServerState.None => Brushes.White,
        //        ServerState.Running => Brushes.Red,
        //        ServerState.Stopped => Brushes.Wheat,
        //        ServerState.AllConnected => Brushes.WhiteSmoke,
        //        _ => Brushes.White
        //    };

        //    OnPropertyChanged(nameof(BackgroundBrush));
        //}

        public Brush BackgroundBrush { get; set; } = Brushes.Wheat;

        private Game? game;
        private GameGridVMs gameGridVMs = new();

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

            GameGridVMs = await GridBuilder.RebuildCells(Game, CellView.Master, true);
            GamePlayerViewModel?.GameGridVMs = GameGridVMs;
        }

        public override async Task VMSaveAsync()
        {
            //TODO: Überarbeiten ... was muss gespeichert werden

            if (Game == null) return;

            using var ctrlGams = new GamesController();
            await ctrlGams.UpsertAsync(Game);
            await ctrlGams.SaveChangesAsync();

            using var ctrlCells = new GameGridCoordinatesController();
            await ctrlCells.UpsertAsync(Game.GameGridCoordinates.Where(c => c.QuestionBase != null || c.QuestionBaseId != Guid.Empty));
            await ctrlCells.SaveChangesAsync();

            using var ctrlResults = new QuestionResultsController();
            await ctrlResults.UpsertAsync(Game.QuestionResults.Where(c => c.QuestionBase != null || c.QuestionBaseId != Guid.Empty));
            await ctrlResults.SaveChangesAsync();
        }

        private Game? Game
        {
            get => game;
            set
            {
                game = value;
            }
        }

        public async Task<Game?> LoadModel(Guid gameId)
        {
            using var ctrlGames = new GamesController();
            var dbGame = await ctrlGames.GetAsync(gameId);

            if (dbGame == null)
            {
                MessageBox.Show("No game found in Database");
                return null;
            }

            if (dbGame.Players.Count() == 0)
            {
                MessageBox.Show("Cannot start the game without any players. Please add at least one player before starting.", "No Players", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (dbGame.GameGridCoordinates.Count == 0)
            {
                MessageBox.Show("Cannot start the game without any questions assigned. Please assign at least one question to the grid before starting.", "No Questions", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (dbGame.Restart)
            {
                await EditGameViewModel.ResetGameResultsAsync(dbGame);

                using var ctrlGamesAfterReset = new GamesController();
                dbGame = (await ctrlGamesAfterReset.GetAsync(gameId)) ?? throw new Exception("Game could not be loaded");

                dbGame.Restart = false;
                if (dbGame.State != GameState.Finished)
                    dbGame.State = GameState.InProgress;

                await ctrlGamesAfterReset.UpdateAsync(dbGame);
                await ctrlGamesAfterReset.SaveChangesAsync();
            }
            else
            {
                if (dbGame.State != GameState.Finished)
                    dbGame.State = GameState.InProgress;

                await ctrlGames.UpdateAsync(dbGame);
                await ctrlGames.SaveChangesAsync();
            }

            Game = dbGame;
            StaticManager.BuzzerServerViewModel.Game = Game;
            await OnModelChangedAsync();

            InitStatContext(Game);

            return Game;
        }

        public bool IsGameFinished => Game?.GameGridCoordinates.All(c => c.IsDone) ?? false;

        private void InitStatContext(Game game)
        {
            StatsContext.PlayerStatsContextList.Clear();
            StatsContext.Game = game;

            if (game.Players.Count() == 0)
            {
                return;
            }

            foreach (var player in game.Players)
            {
                var context = new PlayerStatsContext()
                {
                    Game = game,
                    Player = player,
                    StatsContext = StatsContext
                };

                StatsContext.PlayerStatsContextList.Add(context);
            }

            StatsContext.UpdateScore(IsGameFinished);
        }

        private async Task OnModelChangedAsync()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Players));

            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(CellHeight));
            OnPropertyChanged(nameof(CellWidth));

            await RebuildCellsAsync();
        }

        public QuestionBase? SelectedQuestion { get; set; }

        public IEnumerable<Player> Players => Game?.Players ?? Enumerable.Empty<Player>();

        private RelayCommand? buzzerServerCommand;
        public ICommand BuzzerServerCommand => buzzerServerCommand ??= new RelayCommand(BuzzerServer);

        private void BuzzerServer(object? commandParameter)
        {
            if (buzzerServerView != null)
            {
                buzzerServerView.Close();
                buzzerServerView = null;
            }

            buzzerServerView = new BuzzerServerView();

            buzzerServerView.DataContext = StaticManager.BuzzerServerViewModel;
            buzzerServerView.Show();
        }

        private ICommand? _cellClickCommand;
        public ICommand CellClickCommand => _cellClickCommand ??= new RelayCommand<GameGridCoordinateViewModel>(OnCellClicked);

        private void OnCellClicked(GameGridCoordinateViewModel? cell)
        {
            if (cell == null || Game == null) return;

            var context = new CurrentQuestionViewModel()
            {
                Coordinate = cell.Coordinate,
                GamePlayerViewModel = GamePlayerViewModel
            };

            var qMaster = new QuestionMasterView()
            {
                DataContext = context
            };

            qMaster.ShowDialog();

            cell.RefreshFromModel();

            StatsContext.UpdateScore(IsGameFinished);
        }

        public int Height
        {
            get => Game?.Height ?? 0;
        }

        public int Width
        {
            get => Game?.Width ?? 0;
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

        private AsyncRelayCommand? openGamePlayerViewCommand;
        public ICommand OpenGamePlayerViewCommand => openGamePlayerViewCommand ??= new AsyncRelayCommand(OpenGamePlayerViewAsync);

        private async Task OpenGamePlayerViewAsync(object? commandParameter)
        {
            var window = new GamePlayerView()
            {
                DataContext = GamePlayerViewModel
            };
            OpenGamePlayerViews.Add(window);
            window.Show();
        }
    }
}