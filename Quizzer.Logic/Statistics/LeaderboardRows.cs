using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;

namespace Quizzer.Logic.Statistics
{
    /// <summary>
    /// Die Zeilen der Bestenliste aus einer Datenbank - am Server ueber die Spiele der Statistik, im WPF ueber alle lokalen
    /// (M37 E03). Eine Abfrage fuer beide, damit lokal und am Server dieselben Zeilen in dieselbe Rechnung gehen.
    /// </summary>
    internal static class LeaderboardRows
    {
        /// <summary>Nur Zeilen mit Rohwert oder Klau; <paramref name="gameIds"/> <c>null</c> heisst alle Spiele der Datenbank.</summary>
        public static async Task<List<LeaderboardRow>> LoadAsync(DataContext context, IEnumerable<Guid>? gameIds, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var results = context.QuestionResults.AsNoTracking().Where(r => r.RecordValue != null || r.Steals > 0);

            if (gameIds != null)
            {
                var ids = gameIds.ToList();
                results = results.Where(r => ids.Contains(r.GameId));
            }

            return await results
                .Select(r => new LeaderboardRow(
                    r.Id,
                    r.PlayerId,
                    r.Player.DisplayName != "" ? r.Player.DisplayName : r.Player.Designation,
                    r.GameId,
                    r.Game.Designation,
                    context.MinigameQuestions.Where(q => q.Id == r.QuestionBaseId).Select(q => (MinigameKind?)q.Kind).FirstOrDefault(),
                    context.MinigameQuestions.Where(q => q.Id == r.QuestionBaseId).Select(q => q.Variant).FirstOrDefault(),
                    r.RecordValue,
                    r.RecordedAt,
                    r.Steals))
                .ToListAsync(ct);
        }

        /// <summary>Die Bestenliste ueber diese Spiele, gerechnet von <see cref="LeaderboardCalculator"/>.</summary>
        public static async Task<Leaderboard> CalculateAsync(DataContext context, IEnumerable<Guid>? gameIds, CancellationToken ct = default)
            => LeaderboardCalculator.Calculate(await LoadAsync(context, gameIds, ct));
    }
}
