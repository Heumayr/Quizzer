using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Enumerations;
using Quizzer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Quizzer.Views.HelperViewModels
{
    public static class GridBuilder
    {
        public static void RebuildCells(Game game,
            ObservableCollection<GameGridCoordinateViewModel> cellVMs,
            ObservableCollection<HeaderEntryViewModel> columnHeaderVMs,
            ObservableCollection<HeaderEntryViewModel> rowHeaderVMs,
            CellView cellView,
            bool lockHeader = false)
        {
            if (game == null) return;

            int h = game.Height;
            int w = game.Width;

            cellVMs.Clear();
            columnHeaderVMs.Clear();
            rowHeaderVMs.Clear();

            if (h <= 0 || w <= 0)
            {
                //clear model too when invalid
                game.GameGridCoordinates.Clear();
                game.ColumnHeader.Clear();
                game.RowHeader.Clear();
                return;
            }

            // 1) Remove out-of-bounds coordinates from MODEL
            for (int i = game.GameGridCoordinates.Count - 1; i >= 0; i--)
            {
                var c = game.GameGridCoordinates[i];
                if (c.X < 0 || c.X >= w || c.Y < 0 || c.Y >= h)
                    game.GameGridCoordinates.RemoveAt(i);
            }

            // 2) Remove out-of-bounds headers from MODEL
            foreach (var key in game.ColumnHeader.Keys.ToList())
                if (key < 0 || key >= w)
                    game.ColumnHeader.Remove(key);

            foreach (var key in game.RowHeader.Keys.ToList())
                if (key < 0 || key >= h)
                    game.RowHeader.Remove(key);

            // 3) Ensure header entries exist for all indices
            for (int x = 0; x < w; x++)
                if (!game.ColumnHeader.ContainsKey(x))
                    game.ColumnHeader[x] = (x + 1).ToString();

            for (int y = 0; y < h; y++)
                if (!game.RowHeader.ContainsKey(y))
                    game.RowHeader[y] = (y + 1).ToString();

            // 4) Build header VMs (directly read/write dictionaries)
            for (int x = 0; x < w; x++)
                columnHeaderVMs.Add(new HeaderEntryViewModel(game.ColumnHeader, x, isColumnHeader: true, lockHeader));

            for (int y = 0; y < h; y++)
                rowHeaderVMs.Add(new HeaderEntryViewModel(game.RowHeader, y, isColumnHeader: false, lockHeader));

            // 5) Map remaining coords and ensure full matrix exists
            var map = game.GameGridCoordinates
                .GroupBy(c => (c.Y, c.X))
                .ToDictionary(g => g.Key, g => g.First());

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!map.TryGetValue((y, x), out var cell))
                    {
                        cell = new GameGridCoordinate(y, x);
                        game.GameGridCoordinates.Add(cell);
                        map[(y, x)] = cell;
                    }

                    var cellVM = new GameGridCoordinateViewModel(cell);
                    cellVM.CellView = cellView;
                    cell.Game = game; // set reference for easier access in VM
                    cell.CalculatedPoints();
                    cellVMs.Add(cellVM);
                }
            }
        }
    }
}