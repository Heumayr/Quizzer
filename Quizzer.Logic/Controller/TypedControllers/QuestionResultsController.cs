using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class QuestionResultsController : GenericController<QuestionResult>
    {
        public QuestionResultsController()
        {
        }

        public QuestionResultsController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<QuestionResult> SetQueryAttributes(IQueryable<QuestionResult> query, Actions action)
        {
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<QuestionResult> BeforeActionAsync(QuestionResult entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<QuestionResult> AfterActionAsync(QuestionResult entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }

        public async Task<int> DeleteByGameIdAsync(Guid gameId)
        {
            return await EntitySet
                .Where(qr => qr.GameId == gameId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<QuestionResult>?> GetAllResultsForCoordinate(Guid coordinateId)
        {
            var result = await EntitySet.Where(c => c.GameGridCoordinateId == coordinateId).ToListAsync();

            return result;
        }

        /// <summary>
        /// Zaehlt die Ergebniszeilen der genannten Mitspieler.
        /// <para>
        /// Gebraucht wird das vor dem Loeschen: <c>QuestionResult.PlayerId</c> steht auf CASCADE,
        /// mit dem Mitspieler verschwindet also seine gesamte Punktehistorie. Die Rueckfrage soll
        /// das beziffern koennen, statt nur "wirklich entfernen?" zu sagen.
        /// </para>
        /// </summary>
        public async Task<int> CountResultsOfPlayersAsync(IEnumerable<Guid> playerIds)
        {
            var ids = playerIds?.Distinct().ToList() ?? [];

            if (ids.Count == 0)
                return 0;

            return await EntitySet.CountAsync(r => ids.Contains(r.PlayerId));
        }

        /// <summary>
        /// Zaehlt die Ergebniszeilen der genannten Spiele.
        /// <para>
        /// Der dritte Weg, auf dem eine Punktehistorie verschwindet - und der einzige, der bis
        /// hierher unbeziffert blieb: <c>GamesController.BeforeActionAsync</c> raeumt vor dem
        /// Loeschen eines Spiels dessen Ergebnisse, Zellen, Kopfzeilen und Zuordnungen selbst
        /// weg. Das ist noetig (die Fremdschluessel stehen auf NO ACTION), heisst aber, dass ein
        /// versehentlich geloeschtes Spiel den ganzen Abend mitnimmt.
        /// </para>
        /// </summary>
        public async Task<int> CountResultsOfGamesAsync(IEnumerable<Guid> gameIds)
        {
            var ids = gameIds?.Distinct().ToList() ?? [];

            if (ids.Count == 0)
                return 0;

            return await EntitySet.CountAsync(r => ids.Contains(r.GameId));
        }

        /// <summary>
        /// Zaehlt die Ergebniszeilen der genannten Fragen. Dasselbe wie
        /// <see cref="CountResultsOfPlayersAsync"/>, nur fuer den anderen CASCADE-Pfad:
        /// <c>QuestionResult.QuestionBaseId</c>.
        /// </summary>
        public async Task<int> CountResultsOfQuestionsAsync(IEnumerable<Guid> questionIds)
        {
            var ids = questionIds?.Distinct().ToList() ?? [];

            if (ids.Count == 0)
                return 0;

            return await EntitySet.CountAsync(r => ids.Contains(r.QuestionBaseId));
        }
    }
}