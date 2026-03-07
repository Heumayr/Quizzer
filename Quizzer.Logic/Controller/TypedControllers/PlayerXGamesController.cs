using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class PlayerXGamesController : GenericController<PlayerXGame>
    {
        public PlayerXGamesController()
        {
        }

        public PlayerXGamesController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<PlayerXGame> SetQueryAttributes(IQueryable<PlayerXGame> query, Actions action)
        {
            query = query.Include(q => q.Player).Include(q => q.Game);
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<PlayerXGame> BeforeActionAsync(PlayerXGame entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<PlayerXGame> AfterActionAsync(PlayerXGame entity, Actions action)
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