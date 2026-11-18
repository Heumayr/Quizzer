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

        internal const string TeamHasResults = "Ein Team mit Ergebnissen lässt sich nicht löschen, nur umbenennen.";

        /// <summary>
        /// Schließt die Löschungen ab, auch beim Admin (V09): nimmt mit, was an gelöschten Spielen hängt, weist Teams ab, an
        /// denen danach noch ein Ergebnis hinge (R1-1), und streicht die Abweisung jeder Zeile, die trotzdem gelöscht wird -
        /// keine Zeile steht zugleich in <c>Rejected</c> und <c>Deleted</c> (R1-4).
        /// </summary>
        public async Task<List<SyncKey>> SettleDeletesAsync(
            IReadOnlyCollection<SyncRow> acceptedUpserts, IReadOnlyCollection<SyncKey> acceptedDeletes, SyncPushResponse response, CancellationToken ct)
        {
            var alle = acceptedDeletes.Concat(await DependentsOfDeletedGamesAsync(acceptedDeletes, ct)).Distinct().ToList();

            foreach (var team in await TeamsKeepingResultsAsync(acceptedUpserts, alle, ct))
            {
                alle.Remove(team);
                response.Rejected.Add(new SyncRejected { Type = team.Type, Id = team.Id, Reason = SyncRejectReason.Forbidden, Message = TeamHasResults });
            }

            response.Rejected.RemoveAll(r => alle.Contains(new SyncKey(r.Type, r.Id)));
            return alle;
        }

        /// <summary>
        /// Die gelöschten Teams, an denen nach dem Push noch ein Ergebnis hinge: am Server, nicht angenommen gelöscht und
        /// nicht umgestempelt, oder angenommen mit diesem Stempel. Eine Zeile ohne das Feld behält den Stempel des Servers.
        /// </summary>
        private async Task<List<SyncKey>> TeamsKeepingResultsAsync(
            IReadOnlyCollection<SyncRow> acceptedUpserts, IReadOnlyCollection<SyncKey> deletes, CancellationToken ct)
        {
            var teams = deletes.Where(d => d.Type == nameof(GameTeam)).ToList();

            if (teams.Count == 0)
                return [];

            var ids = teams.Select(t => t.Id).ToList();
            var geloescht = deletes.Where(d => d.Type == nameof(QuestionResult)).Select(d => d.Id).ToHashSet();
            var umgestempelt = acceptedUpserts
                .Where(r => r.Type == nameof(QuestionResult) && r.Values.ContainsKey(nameof(QuestionResult.GameTeamId)) && !geloescht.Contains(r.Id))
                .ToDictionary(r => r.Id, r => GetNullableGuid(r, nameof(QuestionResult.GameTeamId)));

            var amServer = await context.QuestionResults.AsNoTracking()
                .Where(r => r.GameTeamId != null && ids.Contains(r.GameTeamId.Value))
                .Select(r => new { r.Id, r.GameTeamId }).ToListAsync(ct);

            var gehalten = amServer.Where(r => !geloescht.Contains(r.Id) && !umgestempelt.ContainsKey(r.Id)).Select(r => r.GameTeamId)
                .Concat(umgestempelt.Values)
                .OfType<Guid>().ToHashSet();

            return teams.Where(t => gehalten.Contains(t.Id)).ToList();
        }
    }
}
