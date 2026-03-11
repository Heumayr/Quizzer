using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class StepXStepsController : GenericController<StepXStep>
    {
        public StepXStepsController()
        {
        }

        public StepXStepsController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<StepXStep> SetQueryAttributes(IQueryable<StepXStep> query, Actions action)
        {
            if ((action & Actions.GetActions) > 0)
            {
                query = query.Include(q => q.From)
                             .Include(q => q.To);
            }

            return base.SetQueryAttributes(query, action);
        }

        protected override Task<StepXStep> BeforeActionAsync(StepXStep entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<StepXStep> AfterActionAsync(StepXStep entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}