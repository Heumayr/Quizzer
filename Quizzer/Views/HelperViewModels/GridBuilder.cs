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
    /// <summary>
    /// Wird geworfen, wenn ein Raster verkleinert werden soll, in dessen wegfallenden Zellen
    /// bereits gespielt wurde. Die Ergebniszeilen verweisen mit NO ACTION auf die Zelle - das
    /// Loeschen scheitert dann in der Datenbank, und der Aufrufer haette einen halben Zustand.
    /// </summary>
    public sealed class GridShrinkBlockedException : Exception
    {
        public GridShrinkBlockedException(int playedCells)
            : base($"Das Spielfeld lässt sich nicht verkleinern: in {playedCells} der "
                 + "wegfallenden Zellen wurde schon gespielt. Zuerst die Ergebnisse "
                 + "zurücksetzen, dann verkleinern.")
        {
            PlayedCells = playedCells;
        }

        /// <summary>Wie viele der wegfallenden Zellen schon Ergebnisse tragen.</summary>
        public int PlayedCells { get; }
    }

    public static class GridBuilder
    {
        public class GameGridVMs
        {
            public List<GameGridCoordinateViewModel> CellVMs { get; set; } = new();
            public List<HeaderEntryViewModel> ColumnHeaderVMs { get; set; } = new();
            public List<HeaderEntryViewModel> RowHeaderVMs { get; set; } = new();
        }

        /// <summary>
        /// Zaehlt, in wie vielen der uebergebenen Zellen schon gespielt wurde.
        /// </summary>
        private static async Task<int> CountPlayedAsync(List<GameGridCoordinate> coordinates)
        {
            if (coordinates.Count == 0)
                return 0;

            using var ctrlResults = new QuestionResultsController();

            var gespielt = 0;

            foreach (var coord in coordinates)
            {
                var ergebnisse = await ctrlResults.GetAllResultsForCoordinate(coord.Id);

                if (ergebnisse != null && ergebnisse.Count > 0)
                    gespielt++;
            }

            return gespielt;
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

            // Eine Zelle, in der schon gespielt wurde, laesst sich nicht loeschen: die
            // Ergebniszeilen verweisen mit NO ACTION darauf. Bis 2026-09-06 lief das Verkleinern
            // eines gespielten Rasters deshalb in einen rohen Fremdschluesselfehler - und weil
            // Breite und Hoehe im Speicher schon geaendert waren, liess sich das Spiel danach
            // gar nicht mehr starten.
            var gespielte = await CountPlayedAsync(coordsToDelete);

            if (gespielte > 0)
            {
                throw new GridShrinkBlockedException(gespielte);
            }

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