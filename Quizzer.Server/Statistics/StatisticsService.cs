using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Server.Auth;

namespace Quizzer.Server.Statistics
{
    /// <summary>
    /// Die Statistik eines Kontos (M9: „seine spiele und statistiken sehen"): jedes Spiel, in dem
    /// es mitgespielt oder das es als Gamemaster angelegt hat, mit Platz und Punkten.
    /// </summary>
    public sealed class StatisticsService
    {
        public async Task<StatisticsResponse> ForAsync(CurrentAccount account, CancellationToken ct)
        {
            await using var context = new DataContext();
            var me = account.PlayerId;

            var player = await context.Players.AsNoTracking().FirstAsync(p => p.Id == me, ct);

            var games = await context.Games.AsNoTracking()
                .Where(g => g.ModeratorPlayerId == me || context.PlayerXGames.Any(x => x.GameId == g.Id && x.PlayerId == me))
                .Select(g => new
                {
                    g.Id,
                    g.Designation,
                    g.State,
                    g.ModeratorPlayerId,
                    MasterName = g.Moderator == null ? "" : (g.Moderator.DisplayName != "" ? g.Moderator.DisplayName : g.Moderator.Designation),
                    Players = context.PlayerXGames.Where(x => x.GameId == g.Id).Select(x => x.PlayerId).ToList(),
                    Cells = context.GameGridCoordinates.Count(c => c.GameId == g.Id && c.QuestionBaseId != null),
                    Played = context.GameGridCoordinates.Count(c => c.GameId == g.Id && c.QuestionBaseId != null && c.IsDone),
                })
                .ToListAsync(ct);

            var gameIds = games.Select(g => g.Id).ToList();

            var totals = await context.QuestionResults.AsNoTracking()
                .Where(r => gameIds.Contains(r.GameId))
                .GroupBy(r => new { r.GameId, r.PlayerId })
                .Select(grp => new
                {
                    grp.Key.GameId,
                    grp.Key.PlayerId,
                    Score = grp.Sum(r => r.Score + r.Correction - r.MinusScore),
                    Right = grp.Sum(r => r.RightCount),
                    Wrong = grp.Sum(r => r.WrongCount),
                })
                .ToListAsync(ct);

            var list = new List<GameStatistic>();

            foreach (var game in games)
            {
                var scores = game.Players
                    .Select(p => totals.FirstOrDefault(t => t.GameId == game.Id && t.PlayerId == p)?.Score ?? 0)
                    .OrderByDescending(s => s)
                    .ToList();

                var mine = totals.FirstOrDefault(t => t.GameId == game.Id && t.PlayerId == me);
                var playsAlong = game.Players.Contains(me);
                var myScore = mine?.Score ?? 0;

                list.Add(new GameStatistic(
                    game.Id,
                    game.Designation,
                    game.MasterName,
                    game.State,
                    game.Players.Count,
                    playsAlong ? 1 + scores.Count(s => s > myScore) : 0,
                    myScore,
                    mine?.Right ?? 0,
                    mine?.Wrong ?? 0,
                    game.Played,
                    game.Cells,
                    game.ModeratorPlayerId == me));
            }

            var played = list.Where(g => g.Rank > 0).ToList();

            return new StatisticsResponse(
                me,
                player.CalculatedDisplayName,
                played.Count,
                played.Count(g => g.Rank == 1 && g.PlayedCells > 0),
                played.Sum(g => g.FinalScore),
                list.Count(g => g.IsGameMaster),
                list.OrderBy(g => g.GameName).ToList());
        }
    }
}
