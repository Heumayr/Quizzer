using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class GamesController : GenericController<Game>
    {
        public GamesController()
        {
        }

        public GamesController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<Game> SetQueryAttributes(IQueryable<Game> query, Actions action)
        {
            if (action == Actions.Get)
            {
                query = query.Include(q => q.Headers)
                 .Include(q => q.QuestionResults)
                 .Include(q => q.GameGridCoordinates).ThenInclude(t => t.QuestionBase).ThenInclude(q => q!.Category)
                 .Include(q => q.PlayerXGames).ThenInclude(t => t.Player);
            }

            return base.SetQueryAttributes(query, action);
        }

        protected override Task<Game> BeforeActionAsync(Game entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<Game> AfterActionAsync(Game entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}