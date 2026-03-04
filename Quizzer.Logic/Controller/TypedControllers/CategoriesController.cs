using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public class CategoriesController : GenericController<Category>
    {
        public CategoriesController()
        {
        }

        public CategoriesController(ControllerBase other) : base(other)
        {
        }

        protected override IQueryable<Category> SetQueryAttributes(IQueryable<Category> query, Actions action)
        {
            return base.SetQueryAttributes(query, action);
        }

        protected override Task<Category> BeforeActionAsync(Category entity, Actions action)
        {
            return base.BeforeActionAsync(entity, action);
        }

        protected override Task<Category> AfterActionAsync(Category entity, Actions action)
        {
            return base.AfterActionAsync(entity, action);
        }
    }
}