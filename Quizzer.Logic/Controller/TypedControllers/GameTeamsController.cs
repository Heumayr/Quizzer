using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Logic.Controller.TypedControllers
{
    /// <summary>Ein Team, an dem schon Ergebnisse hängen, lässt sich nicht löschen - nur umbenennen (M37 T1).</summary>
    public sealed class TeamHasResultsException(string teamName, int results)
        : Exception($"Das Team „{teamName}“ hat schon {results} Ergebnis(se) und lässt sich nur noch umbenennen.")
    {
        public int Results { get; } = results;
    }

    /// <summary>
    /// Teams eines Spiels (M37 T1). Alle drei Fremdschlüssel stehen auf NO ACTION; die Riegel dazu sitzen hier:
    /// Löschen nimmt die Mitglieder am selben DataContext heraus und lehnt ein Team mit Ergebnissen ab.
    /// </summary>
    public class GameTeamsController : GenericController<GameTeam>
    {
        public GameTeamsController()
        {
        }

        public GameTeamsController(ControllerBase other) : base(other)
        {
        }

        /// <summary>Die Teams eines Spiels in ihrer Reihenfolge.</summary>
        public async Task<List<GameTeam>> GetForGameAsync(Guid gameId)
        {
            return await CreateReadQuery(Actions.GetAll)
                .Where(t => t.GameId == gameId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();
        }

        /// <summary>Wie viele Ergebnisse den Stempel dieses Teams tragen - mehr als 0 heißt: nicht löschbar.</summary>
        public async Task<int> CountResultsAsync(Guid teamId)
        {
            return await CurrentContext.QuestionResults.CountAsync(r => r.GameTeamId == teamId);
        }

        /// <summary>
        /// Schreiben prüft die Grenzen (<see cref="TeamGrenzen"/>), bevor die Datenbank es mit einem rohen Fehler tut.
        /// Löschen: erst die Ergebnisse prüfen, dann die Mitglieder herausnehmen - beides vor dem Entfernen, in der
        /// Klammer bis zum Speichern (V05), damit ein gescheitertes Speichern die Zuteilung zurückbringt.
        /// </summary>
        protected override async Task<GameTeam> BeforeActionAsync(GameTeam entity, Actions action)
        {
            if (action is Actions.Insert or Actions.Update && TeamGrenzen.TeamProblem(entity.Name, entity.ColorIndex, entity.SpeakerTurn) is { } problem)
                throw new ArgumentException(problem, nameof(entity));

            if ((action & Actions.DeleteActions) > 0)
            {
                var ergebnisse = await CountResultsAsync(entity.Id);

                if (ergebnisse > 0)
                    throw new TeamHasResultsException(entity.Name, ergebnisse);

                await CurrentContext.EnsureTransactionUntilSaveAsync();

                await CurrentContext.PlayerXGames
                    .Where(x => x.GameTeamId == entity.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.GameTeamId, (Guid?)null));
            }

            return await base.BeforeActionAsync(entity, action);
        }

        /// <summary>
        /// Für das Löschen eines Spiels: die Teams hängen mit NO ACTION am Spiel. Erst nach Zuteilungen und
        /// Ergebnissen aufrufen - die zeigen ebenfalls mit NO ACTION auf das Team.
        /// </summary>
        public async Task<int> DeleteByGameIdAsync(Guid gameId)
        {
            return await EntitySet
                .Where(t => t.GameId == gameId)
                .ExecuteDeleteAsync();
        }
    }
}
