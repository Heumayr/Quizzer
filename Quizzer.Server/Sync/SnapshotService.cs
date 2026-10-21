using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;
using Quizzer.Server.Auth;

namespace Quizzer.Server.Sync
{
    /// <summary>Alles, was ein Konto sehen darf, als eine Liste von Zeilen.</summary>
    public sealed class SnapshotService(TimeProvider time)
    {
        public async Task<SyncSnapshot> CreateAsync(CurrentAccount account, CancellationToken ct)
        {
            await using var store = new SyncStore();
            var context = store.Context;

            var games = Visibility.GameIds(context, account);
            var questions = Visibility.QuestionIds(context, account);

            var rows = new List<SyncRow>();

            await store.AddAsync(rows, context.Categories, ct);
            await store.AddAsync(rows, context.GameThemes, ct);
            await store.AddAsync(rows, context.Players, ct);
            await store.AddAsync(rows, context.QuestionBases.Where(q => questions.Contains(q.Id)), ct);
            await store.AddAsync(rows, context.QuestionStepResources.Where(s => questions.Contains(s.QuestionBaseId)), ct);
            await store.AddAsync(rows, context.Games.Where(g => games.Contains(g.Id)), ct);
            await store.AddAsync(rows, context.Headers.Where(h => games.Contains(h.GameId)), ct);
            await store.AddAsync(rows, context.GameGridCoordinates.Where(c => games.Contains(c.GameId)), ct);
            await store.AddAsync(rows, context.PlayerXGames.Where(x => games.Contains(x.GameId)), ct);
            await store.AddAsync(rows, context.QuestionResults.Where(r => games.Contains(r.GameId)), ct);

            return new SyncSnapshot { CreatedAt = time.GetUtcNow(), Rows = rows };
        }
    }
}
