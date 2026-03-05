using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.ViewModels;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.HelperViewModels;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.GameViews
{
    public class GameMasterViewModel : ViewModelBase
    {
        private BuzzerServerView? _buzzerServerView = null;

        public GameMasterViewModel()
        {
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        protected override Task OnloadAsync() => Task.CompletedTask;

        protected override Task OnClosed()
        {
            try
            {
                _buzzerServerView?.Close();

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

        private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        {
            BackgroundBrush = e switch
            {
                ServerState.None => Brushes.White,
                ServerState.Running => Brushes.Red,
                ServerState.Stopped => Brushes.Wheat,
                ServerState.AllConnected => Brushes.WhiteSmoke,
                _ => Brushes.White
            };

            OnPropertyChanged(nameof(BackgroundBrush));
        }

        public Brush BackgroundBrush { get; set; } = Brushes.Wheat;

        private Game? game;

        public ObservableCollection<GameGridCoordinateViewModel> CellVMs { get; } = new();
        public ObservableCollection<HeaderEntryViewModel> ColumnHeaderVMs { get; } = new();
        public ObservableCollection<HeaderEntryViewModel> RowHeaderVMs { get; } = new();

        public void RebuildCells()
        {
            if (Game == null) return;

            GridBuilder.RebuildCells(Game, CellVMs, ColumnHeaderVMs, RowHeaderVMs, CellView.Master, true);
        }

        public override async Task VMSaveAsync()
        {
            if (Game == null) return;

            using var ctrl = new GamesController();
            await ctrl.UpsertAsync(Game);

            await ctrl.SaveChangesAsync();
        }

        public Game? Game
        {
            get => game;
            set
            {
                game = value;

                OnModelChanged();
            }
        }

        private void OnModelChanged()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Players));

            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(CellHeight));
            OnPropertyChanged(nameof(CellWidth));

            RebuildCells();
        }

        public QuestionBase? SelectedQuestion { get; set; }

        public IEnumerable<Player> Players => Game?.Players ?? Enumerable.Empty<Player>();

        private RelayCommand? buzzerServerCommand;
        public ICommand BuzzerServerCommand => buzzerServerCommand ??= new RelayCommand(BuzzerServer);

        private void BuzzerServer(object? commandParameter)
        {
            if (_buzzerServerView == null)
            {
                _buzzerServerView = new BuzzerServerView();
            }
            _buzzerServerView.DataContext = StaticManager.BuzzerServerViewModel;
            _buzzerServerView.Show();
        }

        private ICommand? _cellClickCommand;
        public ICommand CellClickCommand => _cellClickCommand ??= new RelayCommand<GameGridCoordinateViewModel>(OnCellClicked);

        private void OnCellClicked(GameGridCoordinateViewModel? cell)
        {
            if (cell == null || Game == null) return;

            MessageBox.Show(cell.DisplayText);

            cell.IsDone = true;

            cell.RefreshFromModel();
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
    }
}