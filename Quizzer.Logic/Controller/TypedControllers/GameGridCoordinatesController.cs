using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class GameGridCoordinatesController : GenericController<GameGridCoordinate>
    {
        public GameGridCoordinatesController()
        {
        }

        public GameGridCoordinatesController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<GameGridCoordinate> SetQueryAttributes(IQueryable<GameGridCoordinate> query, Actions action)
        {
            if ((action & Actions.Get) > 0)
            {
                query = query.Include(c => c.QuestionResults);
            }

            return base.SetQueryAttributes(query, action);
        }

        protected override Task<GameGridCoordinate> BeforeActionAsync(GameGridCoordinate entity, Actions action)
        {
            if (Context != null && (action & Actions.WriteActions) > 0)
            {
                if (entity.Game != null)
                    entity.GameId = entity.Game.Id;

                if (entity.QuestionBase != null)
                    entity.QuestionBaseId = entity.QuestionBase.Id;

                if (entity.QuestionBaseId == Guid.Empty)
                    entity.QuestionBaseId = null;
            }

            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<GameGridCoordinate> AfterActionAsync(GameGridCoordinate entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }

        /// <summary>
        /// Liefert die Bezeichnungen der Spiele, in deren Raster die genannte Frage liegt.
        /// <para>
        /// Gebraucht wird das vor dem Loeschen einer Frage:
        /// <c>GameGridCoordinate.QuestionBaseId</c> steht auf NO ACTION, das Loeschen scheitert
        /// also in der Datenbank. Ohne diese Abfrage bekaeme der Spielleiter einen rohen
        /// Fremdschluesselfehler zu sehen.
        /// </para>
        /// </summary>
        public async Task<List<string>> GameNamesUsingQuestionAsync(Guid questionId)
        {
            if (questionId == Guid.Empty)
                return [];

            return await EntitySet
                .Where(c => c.QuestionBaseId == questionId)
                .Select(c => c.Game.Designation)
                .Distinct()
                .ToListAsync();
        }

        public async Task<int> DeleteByGameIdAsync(Guid gameId)
        {
            return await EntitySet.Where(qr => qr.GameId == gameId)
                .ExecuteDeleteAsync();
        }

        public async Task<int> UpdateIsDoneStateOfGame(Guid gameId, bool isDone)
        {
            return await EntitySet.Where(g => g.GameId == gameId)
                .ExecuteUpdateAsync(setters =>
                 setters.SetProperty(g => g.IsDone, isDone));
        }

        public async Task<int> UpdatePhaseOfGame(Guid gameId, int phase)
        {
            return await EntitySet.Where(g => g.GameId == gameId)
                .ExecuteUpdateAsync(setters =>
                 setters.SetProperty(g => g.Phase, phase));
        }
    }
}