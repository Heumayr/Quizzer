using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.ViewModels;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.HelperViewModels;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
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
        public List<Window> OpenGamePlayerViews { get; private set; } = new();

        private List<Window> WindowsForMediaHandle = new();

        public GamePlayerViewModel GamePlayerViewModel { get; private set; } = new();

        public StatsContext StatsContext { get; private set; } = new();

        private BuzzerServerView? buzzerServerView = null;

        public Brush HeaderColumnBrush { get; set; } = StaticResources.HeaderColumnImageBrush;
        public Brush HeaderRowBrush { get; set; } = StaticResources.HeaderRowImageBrush;

        public Brush PlayGroundBackGroundBrush => StaticResources.PlayGroundBackGround;

        public GameMasterViewModel()
        {
            //StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        protected override async Task OnloadAsync()
        {
            await OnModelChangedAsync();
            GamePlayerViewModel.Game = Game;
            GamePlayerViewModel.GameMasterViewModel = this;
        }

        protected override Task OnWindow_SourceInitializedAsync()
        {
            if (Window != null)
            {
                WindowsForMediaHandle.Add(Window);
                MediaPreviewCoordinator.SetOwnerWindows(WindowsForMediaHandle);
            }
            return base.OnWindow_SourceInitializedAsync();
        }

        protected override Task OnClosed()
        {
            try
            {
                MediaPreviewCoordinator.SetOwnerWindows(new List<Window>());

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
            if (Game == null) return;

            using var ctrlGames = new GamesController();
            await ctrlGames.UpsertAsync(Game);
            await ctrlGames.SaveChangesAsync();

            using var ctrlCells = new GameGridCoordinatesController(ctrlGames);
            await ctrlCells.UpsertAsync(Game.GameGridCoordinates);
            await ctrlCells.SaveChangesAsync();
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
                MessageBox.Show("No game found in Database", "No Game", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var errors = new List<string>();

            if (dbGame.ModeratorPlayerId == null || dbGame.ModeratorPlayerId == Guid.Empty || dbGame.Moderator == null)
            {
                errors.Add("Game must have a moderator set.");
            }

            if (!dbGame.Players.Any())
            {
                errors.Add("Cannot start the game without any players. Please add at least one player before starting.");
            }

            if (dbGame.GameGridCoordinates.Count == 0)
            {
                errors.Add("Cannot start the game without any questions assigned. Please assign at least one question to the grid before starting.");
            }

            if (errors.Any())
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine + Environment.NewLine, errors),
                    "Game cannot be started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

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

            await SetPhaseAndSetCoordinatesPhaseAsync(null);
            await OnModelChangedAsync();

            InitStatContext(Game);
            Game.CalculatetThreshold();
            UpdateGameState();
            InitChoosingPlayer();

            await VMSaveAsync();
            return Game;
        }

        public string ChoosingPlayer => GetChoosingPlayerSignature();

        private string GetChoosingPlayerSignature()
        {
            var result = $"Cant't be{Environment.NewLine}evaluated!";

            if (Game == null) return result;

            var player = Players.FirstOrDefault(p => p.Id == Game.CurrentChoosingPlayerId);

            if (player != null)
            {
                result = $"{player.Designation}";
                if (string.IsNullOrEmpty(player.DisplayName) == false)
                {
                    result += $"{Environment.NewLine}{player.DisplayName}";
                }
            }

            return result;
        }

        public void InitChoosingPlayer()
        {
            if (Game == null) return;

            var players = Players.ToArray();
            var playerCount = Players.Count();

            if (Game.RegularChoosingPlayerId == Guid.Empty && playerCount > 0)
            {
                var random = new Random();
                Game.RegularChoosingPlayerId = players[random.Next(playerCount)].Id;
            }

            if (Game.CurrentChoosingPlayerId == Guid.Empty)
                Game.CurrentChoosingPlayerId = Game.RegularChoosingPlayerId;

            OnPropertyChanged(nameof(ChoosingPlayer));
        }

        public async Task SetNextChoosingPlayer(List<Player> cellWinners)
        {
            if (Game == null)
                return;

            if (cellWinners.Count > 0)
            {
                SetChoosingPlayerFromWinners(cellWinners);
            }
            else
            {
                SetNextRegularChoosingPlayer();
            }

            await VMSaveAsync();
            OnPropertyChanged(nameof(ChoosingPlayer));
        }

        private void SetChoosingPlayerFromWinners(List<Player> winners)
        {
            if (Game == null)
                return;

            if (winners.Count == 1)
            {
                Game.CurrentChoosingPlayerId = winners[0].Id;
                return;
            }

            var currentWinnerStillValid = winners.Any(w => w.Id == Game.CurrentChoosingPlayerId);
            if (currentWinnerStillValid)
                return;

            var random = new Random();
            Game.CurrentChoosingPlayerId = winners[random.Next(winners.Count)].Id;
        }

        private void SetNextRegularChoosingPlayer()
        {
            if (Game == null)
                return;

            var players = Players.ToArray();
            if (players.Length == 0)
                return;

            var currentIndex = Array.FindIndex(players, p => p.Id == Game.RegularChoosingPlayerId);

            if (currentIndex == -1)
            {
                Game.CurrentChoosingPlayerId = players[0].Id;
                return;
            }

            var nextIndex = (currentIndex + 1) % players.Length;
            Game.CurrentChoosingPlayerId = players[nextIndex].Id;
            Game.RegularChoosingPlayerId = Game.CurrentChoosingPlayerId;
        }

        public int GamePhase
        {
            get => Game?.Phase ?? -1;
            set
            {
                Game?.Phase = value;
                OnPropertyChanged();
            }
        }

        public bool IsGameFinished => Game?.GameGridCoordinates.All(c => c.IsDone) ?? false;

        private void InitStatContext(Game game)
        {
            StatsContext.PlayerStatsContextList.Clear();
            StatsContext.Game = game;
            StatsContext.GameMasterViewModel = this;

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
                    StatsContext = StatsContext,
                    GameMasterViewModel = this
                };

                StatsContext.PlayerStatsContextList.Add(context);
            }

            StatsContext.UpdateScores(IsGameFinished);
        }

        private async Task OnModelChangedAsync()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Players));
            OnPropertyChanged(nameof(GamePhase));
            OnPropertyChanged(nameof(PhaseHeadline));
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

        //public ICommand CellClickCommand => _cellClickCommand ??= new RelayCommand<GameGridCoordinateViewModel>(OnCellClicked);
        public ICommand CellClickCommand => _cellClickCommand ??= new AsyncRelayCommand((p) => OnCellClickedAsync(p as GameGridCoordinateViewModel));

        private async Task OnCellClickedAsync(GameGridCoordinateViewModel? cell)
        {
            if (cell == null || Game == null) return;

            var context = new CurrentQuestionViewModel()
            {
                Coordinate = cell.Coordinate,
                GamePlayerViewModel = GamePlayerViewModel,
                GameMasterViewModel = this,
            };

            var qMaster = new QuestionMasterView()
            {
                DataContext = context
            };

            qMaster.ShowDialog();

            cell.RefreshFromModel();

            StatsContext.UpdateScores(IsGameFinished);

            if (context.SetNextChoosingPlayer)
            {
                await SetNextChoosingPlayer(context.CoordinateCorrectedAnsweredPlayers);
            }

            UpdateGameState();

            // War das die letzte Zelle, wechselt der Spielerbildschirm auf die Siegerehrung.
            // Bis 2026-09-06 geschah das nur, wenn der Spielleiter den Punktestand danach von
            // Hand umschaltete - sonst blieb das leere Raster stehen.
            GamePlayerViewModel?.RefreshFinishState();
        }

        public int CurrentRound
        {
            get => Game?.CurrentRound ?? 0;
            set
            {
                Game?.CurrentRound = value;
                OnPropertyChanged();
            }
        }

        public int GameGridCoordinatesCount => Game?.GameGridCoordinates.Count() ?? 0;
        public int GameGridCoordinatesDoneCount => Game?.GameGridCoordinates.Count(c => c.IsDone) ?? 0;

        /// <summary>Titel des Fensters, mit dem Namen des Spiels.</summary>
        public string WindowTitle =>
            string.IsNullOrWhiteSpace(Game?.Designation)
                ? "Spielleitung"
                : $"Spielleitung – {Game!.Designation}";

        /// <summary>
        /// Wie weit das Spiel ist, in einem Satz. Bis 2026-09-06 standen hier zwei Angaben
        /// nebeneinander - "Round" und "Done" -, die dieselbe Zahl mit Versatz eins zeigten.
        /// </summary>
        public string ProgressHeadline =>
            GameGridCoordinatesCount == 0
                ? "Kein Spielfeld"
                : $"Frage {Math.Min(GameGridCoordinatesDoneCount + 1, GameGridCoordinatesCount)} von {GameGridCoordinatesCount}";

        public string OpenCellsText
        {
            get
            {
                var offen = GameGridCoordinatesCount - GameGridCoordinatesDoneCount;

                return offen switch
                {
                    <= 0 => "Alle Fragen gespielt",
                    1 => "Noch eine offen",
                    _ => $"Noch {offen} offen",
                };
            }
        }

        /// <summary>Die Phase samt der Zahl, auf die das Spiel angelegt ist.</summary>
        public string PhaseHeadline
        {
            get
            {
                if (Game == null)
                    return "Keine Phase";

                return Game.SuggestedPhases > 0
                    ? $"Phase {Game.Phase} von {Game.SuggestedPhases}"
                    : $"Phase {Game.Phase}";
            }
        }

        private void UpdateGameState()
        {
            if (Game == null) return;

            var tempCurrentRound = Game.CurrentRound;
            CurrentRound = GameGridCoordinatesDoneCount + 1;

            if (tempCurrentRound != CurrentRound && CurrentRound > tempCurrentRound)
            {
                if (Game.PhaseTrashholds.Contains(CurrentRound))
                {
                    var advance = UserPrompt.Confirm("Punkteschwelle erreicht. Zur nächsten Phase wechseln?", "Phasenschwelle");

                    if (advance)
                    {
                        Game.RaisePhase();
                        _ = SaveAndRefreshAfterPhaseChangeAsync();
                    }
                }
            }

            OnPropertyChanged(nameof(GameGridCoordinatesCount));
            OnPropertyChanged(nameof(GameGridCoordinatesDoneCount));
            OnPropertyChanged(nameof(ProgressHeadline));
            OnPropertyChanged(nameof(OpenCellsText));
            OnPropertyChanged(nameof(PhaseHeadline));
            OnPropertyChanged(nameof(WindowTitle));
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
            // Ein zweites Spieleransichts-Fenster teilt sich das ViewModel mit dem ersten und
            // ueberschreibt dessen Fensterverweis - danach folgt nur noch eines dem Spiel, und
            // welches, ist Zufall. Stattdessen das vorhandene nach vorne holen.
            var vorhanden = OpenGamePlayerViews.FirstOrDefault(w => w.IsLoaded);

            if (vorhanden != null)
            {
                vorhanden.Activate();
                return;
            }

            var window = new GamePlayerView()
            {
                DataContext = GamePlayerViewModel
            };

            window.ContentRendered += (s, e) =>
            {
                WindowsForMediaHandle.Add(window);
                MediaPreviewCoordinator.SetOwnerWindows(WindowsForMediaHandle);
            };

            OpenGamePlayerViews.Add(window);

            window.Show();
        }

        /// <summary>
        /// Beschriftung des Knopfes, der den Punktestand bei den Spielern ein- und ausblendet.
        /// Sie sagt, was der Druck bewirkt - nicht, was gerade gilt.
        /// </summary>
        public string ToggleStatsButtonText
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        } = "Punktestand ausblenden";

        // Voreingestellt sichtbar, ebenso wie im GamePlayerViewModel: der Punktestand ist die
        // Auskunft, nach der die Mitspieler am haeufigsten fragen.
        private bool showStats = true;

        private RelayCommand? togglePlayerStatsViewCommand;
        public ICommand TogglePlayerStatsViewCommand => togglePlayerStatsViewCommand ??= new RelayCommand(TogglePlayerStatsView);

        private void TogglePlayerStatsView(object? commandParameter)
        {
            showStats = !showStats;
            ToggleStatsButtonText = showStats ? "Punktestand ausblenden" : "Punktestand einblenden";
            GamePlayerViewModel.SetShowPlayerStats(showStats);
        }

        private int statsRowHeight = 1;

        public string StatsRowHeight
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        } = "*";

        private RelayCommand? changeViewCommand;
        public ICommand ChangeViewCommand => changeViewCommand ??= new RelayCommand(ChangeView);

        private void ChangeView(object? commandParameter)
        {
            if (statsRowHeight == 1)
            {
                statsRowHeight = 4;
            }
            else
            {
                statsRowHeight = 1;
            }

            StatsRowHeight = $"{statsRowHeight}*";
        }

        private AsyncRelayCommand? raiseGamePhaseCommand;
        public ICommand RaiseGamePhaseCommand => raiseGamePhaseCommand ??= new AsyncRelayCommand(RaiseGamePhaseAsync);

        private async Task RaiseGamePhaseAsync(object? commandParameter)
        {
            if (Game == null) return;
            Game.RaisePhase();
            await SaveAndRefreshAfterPhaseChangeAsync();
        }

        private AsyncRelayCommand? lowerGamePhaseCommand;
        public ICommand LowerGamePhaseCommand => lowerGamePhaseCommand ??= new AsyncRelayCommand(LowerGamePhaseAsync);

        private async Task LowerGamePhaseAsync(object? commandParameter)
        {
            if (Game == null) return;
            Game.LowerPhase();
            await SaveAndRefreshAfterPhaseChangeAsync();
        }

        private async Task SetPhaseAndSetCoordinatesPhaseAsync(object? commandParameter)
        {
            if (Game == null) return;

            Game.SetPhaseAndSetCoordinatesPhase(Game.Phase);
            await SaveAndRefreshAfterPhaseChangeAsync();
        }

        private async Task SaveAndRefreshAfterPhaseChangeAsync()
        {
            await VMSaveAsync();
            foreach (var cell in GameGridVMs.CellVMs)
            {
                cell.RefreshFromModel();
            }
            OnPropertyChanged(nameof(GamePhase));
            OnPropertyChanged(nameof(PhaseHeadline));
        }

        private RelayCommand? toggleFullScreenCommand;
        public ICommand ToggleFullScreenCommand => toggleFullScreenCommand ??= new RelayCommand(ToggleFullScreen);

        public bool IsFullScreen { get; private set; } = false;

        private void ToggleFullScreen(object? commandParameter)
        {
            IsFullScreen = !IsFullScreen;

            foreach (var win in WindowsForMediaHandle)
            {
                if (win is not WindowBase winbase) continue;

                winbase.SetFullscreen(IsFullScreen);
            }

            MediaPreviewCoordinator.FullScreenPreviewEnabled = IsFullScreen;
        }
    }
}