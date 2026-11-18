using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Context;

namespace Quizzer.Server.Statistics
{
    /// <summary>Das Team des Kontos in einem Teamspiel: Name, Platz (Gleichstand teilt ihn) und wie viele Teams es gibt.</summary>
    internal sealed record TeamStand(string Name, int Rank, int Count);

    public sealed partial class StatisticsService
    {
        /// <summary>
        /// Je Teamspiel das Team des Kontos und sein Platz (M37 T12). Gerechnet wird nicht hier, sondern wie am Beamer
        /// (<see cref="Teamwertung.Rangfolge"/>, Stempel vor Zuteilung, F-M37-07) - auf einem Gerüst des Spiels aus den
        /// summierten Ergebnissen je Spieler und Stempel. Teams, Plätze und Ergebnisse kommen nur aus den übergebenen,
        /// sichtbaren Spielen; ein Platz, der auf ein Team eines anderen Spiels zeigt, zählt nirgends (A01).
        /// </summary>
        private static async Task<Dictionary<Guid, TeamStand>> TeamStaendeAsync(
            DataContext context, IReadOnlyCollection<Guid> teamspiele, Guid me, CancellationToken ct)
        {
            if (teamspiele.Count == 0)
                return [];

            var teams = await context.GameTeams.AsNoTracking().Where(t => teamspiele.Contains(t.GameId)).ToListAsync(ct);
            var plaetze = await context.PlayerXGames.AsNoTracking().Where(x => teamspiele.Contains(x.GameId)).ToListAsync(ct);
            var summen = await context.QuestionResults.AsNoTracking()
                .Where(r => teamspiele.Contains(r.GameId))
                .GroupBy(r => new { r.GameId, r.PlayerId, r.GameTeamId })
                .Select(grp => new { grp.Key.GameId, grp.Key.PlayerId, grp.Key.GameTeamId, Score = grp.Sum(r => r.Score + r.Correction - r.MinusScore) })
                .ToListAsync(ct);

            var staende = new Dictionary<Guid, TeamStand>();

            foreach (var spielId in teamspiele)
            {
                var geruest = new Game { Id = spielId, IsTeamGame = true };
                geruest.GameTeams.AddRange(teams.Where(t => t.GameId == spielId));
                geruest.PlayerXGames.AddRange(plaetze.Where(x => x.GameId == spielId));
                geruest.GameGridCoordinates.Add(new GameGridCoordinate
                {
                    GameId = spielId,
                    QuestionResults = summen.Where(s => s.GameId == spielId)
                        .Select(s => new QuestionResult { GameId = spielId, PlayerId = s.PlayerId, GameTeamId = s.GameTeamId, Score = s.Score })
                        .ToList(),
                });

                if (Teamzuordnung.TeamVon(geruest, me) is not { } meinTeam)
                    continue;

                var rangfolge = Teamwertung.Rangfolge(geruest);
                var platz = rangfolge.Single(p => p.Team.Id == meinTeam);
                staende[spielId] = new TeamStand(platz.Team.Name, platz.Platz, rangfolge.Count);
            }

            return staende;
        }
    }
}
