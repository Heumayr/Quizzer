using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
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

        public ObservableCollection<GameGridCoordinate> Cells { get; } = new();

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

        private ICommand? _cellClickCommand;
        public ICommand CellClickCommand => _cellClickCommand ??= new RelayCommand<GameGridCoordinate>(OnCellClicked);

        private void OnCellClicked(GameGridCoordinate? cell)
        {
            if (cell == null) return;

            cell.Question = Loader.Questions.FirstOrDefault();
            cell.QuestionsId = cell.Question?.Id ?? Guid.Empty;
            MessageBox.Show($"Clicked cell at (Y={cell.Y}, X={cell.X})");
        }

        private void RebuildCells()
        {
            if (Game == null) return;

            int h = Game.Height;
            int w = Game.Width;

            Cells.Clear();

            if (h <= 0 || w <= 0) return;

            // map existing coords by (Y,X)
            var map = Game.GameGridCoordinates
                .GroupBy(c => (c.Y, c.X))
                .ToDictionary(g => g.Key, g => g.First());

            // ensure full matrix exists in Game.GameGridCoordinates
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

                    // IMPORTANT: Cells contains SAME objects as Game.GameGridCoordinates
                    Cells.Add(cell);
                }
            }

            // optional: remove coords outside bounds from the model collection
            //for (int i = Game.GameGridCoordinates.Count - 1; i >= 0; i--)
            //{
            //    var c = Game.GameGridCoordinates[i];
            //    if (c.Y >= h || c.X >= w)
            //        Game.GameGridCoordinates.RemoveAt(i);
            //}
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Width));
            RebuildCells();
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
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

        private void OnDatagridSourceChanged()
        {
            //if (Game == null)
            //{
            //    return;
            //}

            //var view = CollectionViewSource.GetDefaultView(Game.QuestionResults);
            //view.SortDescriptions.Clear();
            //view.SortDescriptions.Add(
            //    new System.ComponentModel.SortDescription(
            //        nameof(QuestionResult.Game.Designation),
            //        System.ComponentModel.ListSortDirection.Ascending));

            //view.Refresh();
        }
    }
}