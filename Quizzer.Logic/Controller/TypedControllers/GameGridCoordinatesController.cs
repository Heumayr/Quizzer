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
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<GameGridCoordinate> BeforeActionAsync(GameGridCoordinate entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<GameGridCoordinate> AfterActionAsync(GameGridCoordinate entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}