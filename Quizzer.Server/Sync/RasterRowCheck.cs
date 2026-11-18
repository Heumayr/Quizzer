using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Raster und Phasen im Abgleich (M37 P7, P5-4): nie negativ - nach <see cref="Rastergrenzen"/>, derselben Stelle wie das Bündel.
    /// Kostet nur die Zeile (<c>Invalid</c>), wie <see cref="PunkteRowCheck"/>.
    /// </summary>
    internal static class RasterRowCheck
    {
        /// <summary>Die Beanstandung oder <c>null</c>; andere Zeilen als Spiele gehen unbesehen durch.</summary>
        public static string? Pruefe(SyncRow row)
            => row.Type == nameof(Game) ? PunkteRowCheck.NegativesFeld(row, Rastergrenzen.SpielFelder) : null;
    }
}
