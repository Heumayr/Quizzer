using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class QuestionBasesController : GenericController<QuestionBase>
    {
        public QuestionBasesController()
        {
        }

        public QuestionBasesController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<QuestionBase> SetQueryAttributes(IQueryable<QuestionBase> query, Actions action)
        {
            if ((action & Actions.GetActions) > 0)
            {
                query = query.Include(q => q.Category);
            }

            if (action == Actions.Get)
            {
                query = query.Include(q => q.Steps);
            }

            return base.SetQueryAttributes(query, action);
        }

        protected override Task<QuestionBase> BeforeActionAsync(QuestionBase entity, Actions action)
        {
            if (Context != null && (action & Actions.WriteActions) > 0)
            {
                if (entity.Category != null)
                {
                    entity.CategoryId = entity.Category.Id;

                    entity.Category = null;
                }

                //entity.Steps = null!;
            }

            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<QuestionBase> AfterActionAsync(QuestionBase entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}