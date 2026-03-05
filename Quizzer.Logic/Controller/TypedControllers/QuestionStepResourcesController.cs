using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class QuestionStepResourcesController : GenericController<QuestionStepResource>
    {
        public QuestionStepResourcesController()
        {
        }

        public QuestionStepResourcesController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<QuestionStepResource> SetQueryAttributes(IQueryable<QuestionStepResource> query, Actions action)
        {
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<QuestionStepResource> BeforeActionAsync(QuestionStepResource entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<QuestionStepResource> AfterActionAsync(QuestionStepResource entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}