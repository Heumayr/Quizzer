using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Teams am Server (M37 T1) - eigene Teildatei wie <c>WriteRules.Joker.cs</c>. Das Team selbst schreibt, wer das Spiel
    /// schreiben darf (der <c>default</c>-Zweig: altes und neues Spiel), und es bleibt in seinem Spiel (V15). Was hier
    /// steht, ist Zusammenhang und gilt auch für den Admin (V09).
    /// </summary>
    internal sealed partial class WriteRules
    {
        /// <summary>Dieselbe Meldung für „gibt es nicht" und „gehört zu einem anderen Spiel" - kein Orakel über fremde Teams.</summary>
        internal const string TeamNotInGame = "Das Team gibt es in diesem Spiel nicht.";

        /// <summary>
        /// Zuteilung und Ergebnis zeigen auf ein Team DIESES Spiels - am Server oder neu im selben Push. Steht das Team am
        /// Server, entscheidet sein Spiel dort, nicht eine Zeile im Push (wie <see cref="CellBelongsToGameAsync"/>).
        /// </summary>
        private async Task<string?> CheckTeamRefAsync(SyncRow row, Guid gameId, CancellationToken ct)
        {
            if (GetNullableGuid(row, nameof(PlayerXGame.GameTeamId)) is not { } teamId)
                return null;

            var amServer = await context.GameTeams.AsNoTracking()
                .Where(t => t.Id == teamId).Select(t => (Guid?)t.GameId).FirstOrDefaultAsync(ct);

            if (amServer != null)
                return amServer == gameId ? null : TeamNotInGame;

            return request.Upserts.Any(u => u.Row.Type == nameof(GameTeam) && u.Row.Id == teamId
                && GetGuid(u.Row, nameof(GameTeam.GameId)) == gameId)
                ? null
                : TeamNotInGame;
        }

        /// <summary>
        /// Ein Team mit Ergebnissen lässt sich nur umbenennen - auch beim Admin, sonst scheiterte der ganze Push am NO
        /// ACTION. Ergebnisse, die derselbe Push löscht, zählen nicht (ein Spiel samt Teams und Ergebnissen löschen).
        /// </summary>
        private async Task<string?> CheckTeamDeleteAsync(object serverEntity, CancellationToken ct)
        {
            if (serverEntity is not GameTeam team)
                return null;

            var mitGeloescht = request.Deletes.Where(d => d.Type == nameof(QuestionResult)).Select(d => d.Id).ToList();

            return await context.QuestionResults.AsNoTracking().AnyAsync(r => r.GameTeamId == team.Id && !mitGeloescht.Contains(r.Id), ct)
                ? "Ein Team mit Ergebnissen lässt sich nicht löschen, nur umbenennen."
                : null;
        }
    }
}
