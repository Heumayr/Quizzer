using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Sync
{
    /// <summary>Was ein gelöschtes Spiel mitnimmt - eigene Teildatei, damit <c>PushService.cs</c> unter 500 Zeilen bleibt (M37 T1).</summary>
    internal sealed partial class WriteRules
    {
        /// <summary>
        /// Ein geloeschtes Spiel nimmt alles mit, was an ihm haengt - die Fremdschluessel stehen
        /// auf NO ACTION, also muss der Server es selbst tun (wie <c>GamesController</c> lokal).
        /// Die Reihenfolge ergibt der Rang beim Einspielen; seit M37 T1 gehen die Teams mit.
        /// </summary>
        public async Task<List<SyncKey>> DependentsOfDeletedGamesAsync(IReadOnlyCollection<SyncKey> deletes, CancellationToken ct)
        {
            var gameIds = deletes.Where(d => d.Type == nameof(Game)).Select(d => d.Id).ToList();

            if (gameIds.Count == 0)
                return [];

            var keys = new List<SyncKey>();

            keys.AddRange((await context.QuestionResults.Where(r => gameIds.Contains(r.GameId)).Select(r => r.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(QuestionResult), id)));
            keys.AddRange((await context.PlayerJokers.Where(j => gameIds.Contains(j.GameId)).Select(j => j.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(PlayerJoker), id)));
            keys.AddRange((await context.PlayerXGames.Where(x => gameIds.Contains(x.GameId)).Select(x => x.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(PlayerXGame), id)));
            keys.AddRange((await context.GameGridCoordinates.Where(c => gameIds.Contains(c.GameId)).Select(c => c.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(GameGridCoordinate), id)));
            keys.AddRange((await context.Headers.Where(h => gameIds.Contains(h.GameId)).Select(h => h.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(Header), id)));
            keys.AddRange((await context.GameTeams.Where(t => gameIds.Contains(t.GameId)).Select(t => t.Id).ToListAsync(ct))
                .Select(id => new SyncKey(nameof(GameTeam), id)));

            return keys;
        }
    }
}
