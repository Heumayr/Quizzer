using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using System;
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
            query = query.Include(q => q.Category);

            return base.SetQueryAttributes(query, action);
        }

        protected override Task<QuestionBase> BeforeActionAsync(QuestionBase entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<QuestionBase> AfterActionAsync(QuestionBase entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}