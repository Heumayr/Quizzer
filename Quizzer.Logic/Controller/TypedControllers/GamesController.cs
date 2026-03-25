using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
                query = query.AsNoTracking().Include(q => q.Headers)
                 .Include(q => q.GameGridCoordinates).ThenInclude(t => t.QuestionBase).ThenInclude(q => q!.Category)
                 .Include(q => q.GameGridCoordinates).ThenInclude(t => t.QuestionResults)
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
            if (action == Actions.Get)
            {
                foreach (var result in entity.QuestionResults)
                {
                    // set up references after load
                    result.Game = entity;
                    result.Player = entity.Players.FirstOrDefault(p => p.Id == result.PlayerId)!; //must be existend!
                    result.GameGridCoordinate = entity.GameGridCoordinates.FirstOrDefault(c => c.Id == result.GameGridCoordinateId)!; //must be existend!
                    result.QuestionBase = result.GameGridCoordinate.QuestionBase!; //must be existend!
                }
            }

            return base.AfterActionAsync(entity, action);
        }
    }
}