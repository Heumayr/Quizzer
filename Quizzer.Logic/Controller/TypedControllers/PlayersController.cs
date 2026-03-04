using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class PlayersController : GenericController<Player>
    {
        public PlayersController()
        {
        }

        public PlayersController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<Player> SetQueryAttributes(IQueryable<Player> query, Actions action)
        {
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<Player> BeforeActionAsync(Player entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<Player> AfterActionAsync(Player entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}