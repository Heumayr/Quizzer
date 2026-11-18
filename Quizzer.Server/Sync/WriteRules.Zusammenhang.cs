using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Der Zusammenhang einer Zeile (M37 D2, V09) - er gilt für jeden, auch für den Admin; bei ihm entfallen nur die
    /// Rechte. Gemessen 2026-11-16: ein Ergebnis und ein Joker aus Spiel A an einer Zelle von B kamen über den Admin
    /// durch; B ließ sich danach nicht mehr löschen (NO ACTION), und das Abholen des Gamemasters von A brach ab.
    /// Ebenso bleibt eine bestehende Zeile in ihrem Spiel (V15).
    /// </summary>
    internal sealed partial class WriteRules
    {
        private async Task<string?> CheckCoherenceAsync(SyncRow row, object? serverEntity, CancellationToken ct)
        {
            var gameId = GetGuid(row, "GameId");

            // Zelle, Kopfzeile, Platz, Ergebnis, Joker wechseln nie das Spiel (V15). Gemessen: wer zwei Spiele leitet,
            // verschob eine Zelle samt Ergebnis von A nach A2 - danach ließ sich A2 nicht mehr löschen.
            if (serverEntity != null && context.Entry(serverEntity).Metadata.FindProperty("GameId") != null
                && (Guid)context.Entry(serverEntity).Property("GameId").CurrentValue! != gameId)
                return "Eine Zeile lässt sich nicht in ein anderes Spiel verschieben.";

            // Ein Zellverweis (Ergebnis, Joker) zeigt in DASSELBE Spiel (Sicherheitspruefung 2026-10-21).
            if (row.Values.ContainsKey(nameof(QuestionResult.GameGridCoordinateId))
                && GetNullableGuid(row, nameof(QuestionResult.GameGridCoordinateId)) is { } cellId
                && !await CellBelongsToGameAsync(cellId, gameId, ct))
                return row.Type == nameof(PlayerJoker)
                    ? "Der Joker verweist auf eine Zelle eines anderen Spiels."
                    : "Das Ergebnis verweist auf eine Zelle eines anderen Spiels.";

            var art = row.Type switch
            {
                nameof(PlayerJoker) => await CheckJokerAsync(row, gameId, ct),

                // Zuteilung und Ergebnis zeigen auf ein Team desselben Spiels (M37 T1).
                nameof(PlayerXGame) or nameof(QuestionResult) => await CheckTeamRefAsync(row, gameId, ct),
                _ => null,
            };

            // Nach der Joker-Regel, damit deren eigene Meldung bleibt (V21).
            return art ?? CheckEnumRange(row);
        }
    }
}
