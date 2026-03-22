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
    }
}