using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class HeadersController : GenericController<Header>
    {
        public HeadersController()
        {
        }

        public HeadersController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<Header> SetQueryAttributes(IQueryable<Header> query, Actions action)
        {
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<Header> BeforeActionAsync(Header entity, Actions action)
        {
            if (Context != null && (action & Actions.WriteActions) > 0)
            {
                if (entity.Game != null)
                {
                    entity.GameId = entity.Game.Id;
                }
            }

            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<Header> AfterActionAsync(Header entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }

        public async Task<int> DeleteByGameIdAsync(Guid gameId)
        {
            return await EntitySet
                .Where(qr => qr.GameId == gameId)
                .ExecuteDeleteAsync();
        }
    }
}