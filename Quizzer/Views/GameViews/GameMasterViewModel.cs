using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Enumerations;
using Quizzer.ViewModels;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.HelperViewModels;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.GameViews
{
    public class GameMasterViewModel : ViewModelBase
    {
        public GameMasterViewModel()
        {
            StaticManager.BuzzerServerViewModel.PlayerConnectionStateChanged += OnPlayerConnectionStateChanged;
        }

        private void OnPlayerConnectionStateChanged(object? sender, ServerState e)
        {
            BackgroundBrush = e switch
            {
                ServerState.Unknown => Brushes.White,
                ServerState.Running => Brushes.Red,
                ServerState.Stopped => Brushes.Wheat,
                ServerState.AllConnecteted => Brushes.WhiteSmoke,
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
            var gCtrl = new GenericDataHandler();
            await gCtrl.SaveToFileAsync(Loader.Games);
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

        public List<Player> Players => Game?.Players ?? new List<Player>();

        private RelayCommand? buzzerServerCommand;
        public ICommand BuzzerServerCommand => buzzerServerCommand ??= new RelayCommand(BuzzerServer);

        private void BuzzerServer(object? commandParameter)
        {
            var window = new BuzzerServerView();
            window.DataContext = StaticManager.BuzzerServerViewModel;
            window.Show();
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