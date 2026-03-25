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

        public static async Task<GameGridVMs> RebuildCells(
    Game game,
    CellView cellView,
    bool lockHeader = false)
        {
            var result = new GameGridVMs();

            if (game == null) return result;

            int h = game.Height;
            int w = game.Width;

            using var ctrlHeader = new HeadersController();
            using var ctrlCoords = new GameGridCoordinatesController();

            // -------------------------------------------------
            // 1) Delete headers outside current bounds
            // -------------------------------------------------
            var headersToDelete = game.Headers
                .Where(hd =>
                    (hd.HeaderType == HeaderType.Column && (hd.Index < 1 || hd.Index > w)) ||
                    (hd.HeaderType == HeaderType.Row && (hd.Index < 1 || hd.Index > h)))
                .ToList();

            foreach (var header in headersToDelete)
            {
                await ctrlHeader.DeleteAsync(header.Id); // ggf. auf DeleteAsync(header.Id) anpassen
                game.Headers.Remove(header);
            }

            if (headersToDelete.Count > 0)
            {
                await ctrlHeader.SaveChangesAsync();
            }

            // -------------------------------------------------
            // 2) Delete coordinates outside current bounds
            // -------------------------------------------------
            var coordsToDelete = game.GameGridCoordinates
                .Where(c => c.Y < 0 || c.Y >= h || c.X < 0 || c.X >= w)
                .ToList();

            foreach (var coord in coordsToDelete)
            {
                await ctrlCoords.DeleteAsync(coord.Id); // ggf. auf DeleteAsync(coord.Id) anpassen
                game.GameGridCoordinates.Remove(coord);
            }

            if (coordsToDelete.Count > 0)
            {
                await ctrlCoords.SaveChangesAsync();
            }

            // -------------------------------------------------
            // 3) Ensure column headers exist
            // -------------------------------------------------
            for (int i = 1; i <= w; i++)
            {
                var found = game.Columns.FirstOrDefault(c => c.Index == i);
                if (found == null)
                {
                    var inserted = await ctrlHeader.InsertAsync(new Header()
                    {
                        GameId = game.Id,
                        HeaderType = HeaderType.Column,
                        Index = i,
                        Designation = i.ToString()
                    });

                    await ctrlHeader.SaveChangesAsync();

                    found = inserted?.Entity;
                    if (found == null)
                        throw new Exception("Not able to find column header.");

                    game.Headers.Add(found);
                }

                result.ColumnHeaderVMs.Add(new HeaderEntryViewModel(found, isColumnHeader: true, lockHeader));
            }

            result.ColumnHeaderVMs = result.ColumnHeaderVMs
                .OrderBy(c => c.Index)
                .ToList();

            // -------------------------------------------------
            // 4) Ensure row headers exist
            // -------------------------------------------------
            for (int i = 1; i <= h; i++)
            {
                var found = game.Rows.FirstOrDefault(r => r.Index == i);
                if (found == null)
                {
                    var inserted = await ctrlHeader.InsertAsync(new Header()
                    {
                        GameId = game.Id,
                        HeaderType = HeaderType.Row,
                        Index = i,
                        Designation = i.ToString()
                    });

                    await ctrlHeader.SaveChangesAsync();

                    found = inserted?.Entity;
                    if (found == null)
                        throw new Exception("Not able to find row header.");

                    game.Headers.Add(found);
                }

                result.RowHeaderVMs.Add(new HeaderEntryViewModel(found, isColumnHeader: false, lockHeader));
            }

            result.RowHeaderVMs = result.RowHeaderVMs
                .OrderBy(r => r.Index)
                .ToList();

            // -------------------------------------------------
            // 5) Build map from remaining coords
            // -------------------------------------------------
            var map = game.GameGridCoordinates
                .GroupBy(c => (c.Y, c.X))
                .ToDictionary(g => g.Key, g => g.First());

            // -------------------------------------------------
            // 6) Ensure full matrix exists
            // -------------------------------------------------
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

                    cell.Game = game;
                    cell.CalculateAndSetCurrentPoints();

                    var cellVM = new GameGridCoordinateViewModel(cell)
                    {
                        CellView = cellView,
                        ColumnHeader = result.ColumnHeaderVMs.FirstOrDefault(c => c.Index == x + 1),
                        RowHeader = result.RowHeaderVMs.FirstOrDefault(r => r.Index == y + 1)
                    };

                    result.CellVMs.Add(cellVM);
                }
            }

            return result;
        }
    }
}