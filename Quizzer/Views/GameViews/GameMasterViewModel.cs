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

using Quizzer.Logic.Spielablauf;

namespace Quizzer.Views.GameViews
{
    public partial class GameMasterViewModel : ViewModelBase
    {
        public List<Window> OpenGamePlayerViews { get; private set; } = new();

        private List<Window> WindowsForMediaHandle = new();

        public GamePlayerViewModel GamePlayerViewModel { get; private set; } = new();

        /// <summary>V5: der Countdown - einer je Spiel, Fragefenster und Beamer teilen ihn.</summary>
        public CountdownViewModel Countdown { get; } = new();

        public StatsContext StatsContext { get; private set; } = new();

        private BuzzerServerView? buzzerServerView = null;

        public Brush HeaderColumnBrush => ThemeBrushes.Current.SpaltenKopf;
        public Brush HeaderRowBrush => ThemeBrushes.Current.ZeilenKopf;

        public Brush PlayGroundBackGroundBrush => ThemeBrushes.Current.Hintergrund;

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

                StaticManager.BuzzerServerViewModel.PlayerJoined -= OnPlayerJoined;

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

        /// <summary>
        /// Das geladene Spiel. <c>internal</c> statt <c>private</c>, damit eine Zusicherung ein
        /// Raster mit leeren Zellen hineingeben kann, ohne den Umweg ueber die Datenbank.
        /// </summary>
        internal Game? Game
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
                UserPrompt.Inform("Das Spiel wurde in der Datenbank nicht gefunden.", "Spiel öffnen");
                return null;
            }

            if (!await StartbarAsync(dbGame))
                return null;

            dbGame = await ZuruecksetzenWennGewuenschtAsync(gameId, dbGame);

            Game = dbGame;
            StaticManager.BuzzerServerViewModel.Game = Game;

            // M10: wer sich am Telefon anmeldet und bestaetigt wird, gehoert ab sofort zur Wertung.
            StaticManager.BuzzerServerViewModel.PlayerJoined -= OnPlayerJoined;
            StaticManager.BuzzerServerViewModel.PlayerJoined += OnPlayerJoined;

            // Ab hier zeichnen Spielfeld und Beamer mit dem Design dieses Abends. Ohne
            // gewaehltes Design bleibt es beim Auslieferungsstand.
            ThemeBrushes.SetCurrent(dbGame.GameTheme);

            await SetPhaseAndSetCoordinatesPhaseAsync(null);
            await OnModelChangedAsync();

            InitStatContext(Game);
            Game.CalculatetThreshold();
            await UpdateGameStateAsync();
            InitChoosingPlayer();

            await VMSaveAsync();
            return Game;
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

        /// <summary>Titel des Fensters, mit dem Namen des Spiels.</summary>
        public string WindowTitle =>
            string.IsNullOrWhiteSpace(Game?.Designation)
                ? "Spielleitung"
                : $"Spielleitung – {Game!.Designation}";

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
    }
}