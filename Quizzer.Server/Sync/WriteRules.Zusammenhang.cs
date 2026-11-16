using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Der Zusammenhang einer Zeile (M37 D2, V09) - er gilt für jeden, auch für den Admin; bei ihm entfallen nur die
    /// Rechte. Gemessen 2026-11-16: ein Ergebnis und ein Joker aus Spiel A an einer Zelle von B kamen über den Admin
    /// durch; B ließ sich danach nicht mehr löschen (NO ACTION), und das Abholen des Gamemasters von A brach ab.
    /// </summary>
    internal sealed partial class WriteRules
    {
        private async Task<string?> CheckCoherenceAsync(SyncRow row, CancellationToken ct)
        {
            var gameId = GetGuid(row, "GameId");

            // Ein Zellverweis (Ergebnis, Joker) zeigt in DASSELBE Spiel (Sicherheitspruefung 2026-10-21).
            if (row.Values.ContainsKey(nameof(QuestionResult.GameGridCoordinateId))
                && GetNullableGuid(row, nameof(QuestionResult.GameGridCoordinateId)) is { } cellId
                && !await CellBelongsToGameAsync(cellId, gameId, ct))
                return row.Type == nameof(PlayerJoker)
                    ? "Der Joker verweist auf eine Zelle eines anderen Spiels."
                    : "Das Ergebnis verweist auf eine Zelle eines anderen Spiels.";

            return row.Type == nameof(PlayerJoker) ? await CheckJokerAsync(row, gameId, ct) : null;
        }
    }
}
