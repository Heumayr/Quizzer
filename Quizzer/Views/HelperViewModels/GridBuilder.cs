using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace Quizzer.Views.HelperViewModels
{
    public static class GridBuilder
    {
        public class GameGridVMs
        {
            public List<GameGridCoordinateViewModel> CellVMs { get; set; } = new();
            public List<HeaderEntryViewModel> ColumnHeaderVMs { get; set; } = new();
            public List<HeaderEntryViewModel> RowHeaderVMs { get; set; } = new();
        }

        public static async Task<GameGridVMs> RebuildCells(Game game,
            CellView cellView,
            bool lockHeader = false)
        {
            var result = new GameGridVMs();

            if (game == null) return result;

            int h = game.Height;
            int w = game.Width;

            //if (h <= 0 || w <= 0)
            //{
            //    //clear model too when invalid
            //    game.GameGridCoordinates.Clear();
            //    game.Columns.Clear();
            //    game.Rows.Clear();
            //    return;
            //}

            using var ctrlHeader = new HeadersController();
            for (int i = 1; i <= w; i++)
            {
                var found = game.Columns.FirstOrDefault(c => c.Index == i);
                if (found == null)
                {
                    var inserted = await ctrlHeader.InsertAsync(new()
                    {
                        GameId = game.Id,
                        HeaderType = HeaderType.Column,
                        Index = i,
                        Designation = i.ToString()
                    });
                    await ctrlHeader.SaveChangesAsync();

                    found = inserted?.Entity;

                    if (found == null) throw new Exception("Not able to find cloumn header.");

                    game.Headers.Add(found);
                }

                result.ColumnHeaderVMs.Add(new HeaderEntryViewModel(found, isColumnHeader: true, lockHeader));
            }
            result.ColumnHeaderVMs = result.ColumnHeaderVMs.OrderBy(c => c.Index).ToList();

            for (int i = 1; i <= h; i++)
            {
                var found = game.Rows.FirstOrDefault(c => c.Index == i);
                if (found == null)
                {
                    var inserted = await ctrlHeader.InsertAsync(new()
                    {
                        GameId = game.Id,
                        HeaderType = HeaderType.Row,
                        Index = i,
                        Designation = i.ToString()
                    });
                    await ctrlHeader.SaveChangesAsync();

                    found = inserted?.Entity;

                    if (found == null) throw new Exception("Not able to find cloumn header.");

                    game.Headers.Add(found);
                }

                result.RowHeaderVMs.Add(new HeaderEntryViewModel(found, isColumnHeader: false, lockHeader));
            }
            result.RowHeaderVMs = result.RowHeaderVMs.OrderBy(c => c.Index).ToList();

            // 5) Map remaining coords and ensure full matrix exists
            var map = game.GameGridCoordinates
                .GroupBy(c => (c.Y, c.X))
                .ToDictionary(g => g.Key, g => g.First());

            using var ctrlCoords = new GameGridCoordinatesController();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!map.TryGetValue((y, x), out var cell))
                    {
                        var inserted = await ctrlCoords.InsertAsync(new GameGridCoordinate()
                        {
                            Y = y,
                            X = x,
                            QuestionBaseId = null,
                            GameId = game.Id,
                        });
                        await ctrlCoords.SaveChangesAsync();

                        cell = inserted?.Entity ?? throw new Exception("Cell could not be inserted");

                        game.GameGridCoordinates.Add(cell);
                        map[(y, x)] = cell;
                    }

                    var cellVM = new GameGridCoordinateViewModel(cell);
                    cellVM.CellView = cellView;
                    cellVM.ColumnHeader = result.ColumnHeaderVMs.FirstOrDefault(c => c.Index == x + 1);
                    cellVM.RowHeader = result.RowHeaderVMs.FirstOrDefault(r => r.Index == y + 1);
                    cell.Game = game; // set reference for easier access in VM
                    cell.CalculateAndSetCurrentPoints();
                    result.CellVMs.Add(cellVM);
                }
            }

            return result;
        }
    }
}