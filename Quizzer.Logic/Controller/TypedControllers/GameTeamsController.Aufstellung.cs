using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Logic.Controller.TypedControllers
{
    /// <summary>Der frische Stand nach dem Speichern einer Aufstellung - mit RowVersion, zum Übernehmen ins geladene Spiel.</summary>
    public sealed record TeamAufstellung(IReadOnlyList<GameTeam> Teams, IReadOnlyList<PlayerXGame> Plaetze);

    public partial class GameTeamsController
    {
        /// <summary>Dieselbe Meldung für „gibt es nicht“ und „anderes Spiel“ - wie am Server (kein Orakel).</summary>
        private const string FremdesTeam = "Das Team gehört nicht zu diesem Spiel.";

        /// <summary>
        /// Schreibt die Aufstellung eines Spiels in einem Zug (M37 T2): Teams anlegen, ändern und entfernen, dazu die
        /// Zuteilung je Spieler (Spieler-Id → Team-Id, <c>null</c> = ohne Team; wer fehlt, bleibt, wie er ist). Ein entferntes
        /// Team gibt seine Mitglieder sofort frei und öffnet dafür die Klammer bis zum Speichern (<see cref="BeforeActionAsync"/>)
        /// - ein späterer Fehler holt das zurück. Grenzen und fremde Teams werfen <see cref="ArgumentException"/> vor jedem
        /// Schreiben, ein entferntes Team mit Ergebnissen <see cref="TeamHasResultsException"/>. Der Sprecherstand bestehender
        /// Teams bleibt. Schreibt selbst und liefert den frischen Stand.
        /// </summary>
        public async Task<TeamAufstellung> SaveLineupAsync(Guid gameId, IReadOnlyList<GameTeam> teams, IReadOnlyDictionary<Guid, Guid?> zuteilung)
        {
            await PruefeAufstellungAsync(gameId, teams, zuteilung);

            var ids = teams.Select(t => t.Id).ToHashSet();
            var bestand = await EntitySet.Where(t => t.GameId == gameId).ToListAsync();

            foreach (var weg in bestand.Where(t => !ids.Contains(t.Id)))
                await DeleteAsync(weg.Id);

            foreach (var team in teams)
            {
                if (bestand.FirstOrDefault(t => t.Id == team.Id) is { } alt)
                {
                    alt.Name = team.Name;
                    alt.ColorIndex = team.ColorIndex;
                    alt.SortOrder = team.SortOrder;
                    continue;
                }

                await InsertAsync(new GameTeam { Id = team.Id, GameId = gameId, Name = team.Name, ColorIndex = team.ColorIndex, SortOrder = team.SortOrder });
            }

            // Erst nach dem Entfernen geladen: das Freigeben der Mitglieder steht dann schon im frischen Stand.
            var plaetze = await CurrentContext.PlayerXGames.Where(x => x.GameId == gameId).ToListAsync();

            foreach (var (spielerId, teamId) in zuteilung)
            {
                var platz = plaetze.FirstOrDefault(x => x.PlayerId == spielerId)
                    ?? throw new ArgumentException("Ein Spieler der Zuteilung sitzt nicht an diesem Spiel.", nameof(zuteilung));

                platz.GameTeamId = teamId;
            }

            await SaveChangesAsync();

            return new TeamAufstellung(
                await GetForGameAsync(gameId),
                await CurrentContext.PlayerXGames.AsNoTracking().Where(x => x.GameId == gameId).ToListAsync());
        }

        /// <summary>Grenzen, Ids und fremde Teams - vor jedem Schreiben.</summary>
        private async Task PruefeAufstellungAsync(Guid gameId, IReadOnlyList<GameTeam> teams, IReadOnlyDictionary<Guid, Guid?> zuteilung)
        {
            if (TeamGrenzen.AufstellungProblem(teams) is { } problem)
                throw new ArgumentException(problem, nameof(teams));

            if (teams.Any(t => t.Id == Guid.Empty))
                throw new ArgumentException("Ein Team der Aufstellung hat keine Id.", nameof(teams));

            var ids = teams.Select(t => t.Id).ToHashSet();

            if (zuteilung.Values.Any(t => t is { } id && !ids.Contains(id)))
                throw new ArgumentException(FremdesTeam, nameof(zuteilung));

            if (await EntitySet.AnyAsync(t => ids.Contains(t.Id) && t.GameId != gameId))
                throw new ArgumentException(FremdesTeam, nameof(teams));
        }
    }
}
