using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;
using System.Text.Json;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Punkte-Parameter im Abgleich (M37 P5, R3-3): Faktoren und Zuschläge am Spiel, Punkte und Minuspunkte an jeder Frage nie
    /// negativ, ebenso die gespeicherten Punkte einer Zelle (P7, P5-5) - nach <see cref="Punktegrenzen"/>, derselben Stelle wie das
    /// Bündel. Kostet nur die Zeile (<c>Invalid</c>), wie <see cref="TeamRowCheck"/>.
    /// </summary>
    internal static class PunkteRowCheck
    {
        /// <summary>Die Beanstandung oder <c>null</c>; andere Zeilen gehen unbesehen durch.</summary>
        public static string? Pruefe(SyncRow row)
        {
            var felder = row.Type == nameof(Game) ? Punktegrenzen.SpielFelder
                : row.Type == nameof(GameGridCoordinate) ? Punktegrenzen.ZellenFelder
                : SyncModel.TryGet(row.Type, out var eintrag) && SyncModel.IsQuestion(eintrag) ? Punktegrenzen.FrageFelder
                : null;

            return felder == null ? null : NegativesFeld(row, felder);
        }

        /// <summary>Das erste negative der Felder, wie <see cref="Punktegrenzen.Problem"/> es nennt - auch für Raster und Phasen (P5-4).</summary>
        internal static string? NegativesFeld(SyncRow row, IReadOnlyDictionary<string, string> felder)
        {
            foreach (var (feld, beschriftung) in felder)
            {
                // Fehlt ein Feld, bleibt der Stand der Zeile - geprüft wird, was kommt (wie TeamRowCheck).
                if (row.Values.TryGetValue(feld, out var wert) && wert.ValueKind == JsonValueKind.Number && wert.TryGetDouble(out var zahl)
                    && Punktegrenzen.Problem(beschriftung, zahl) is { } problem)
                    return problem;
            }

            return null;
        }
    }
}
