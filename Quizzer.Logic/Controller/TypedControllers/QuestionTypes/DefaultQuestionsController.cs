//using Quizzer.DataModels.Enumerations;
//using Quizzer.DataModels.Models;
//using Quizzer.DataModels.Models.QuestionTypes;
//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace Quizzer.Logic.Controller.TypedControllers.QuestionTypes
//{
//    public class DefaultQuestionsController : GenericController<DefaultQuestion>
//    {
//        public DefaultQuestionsController()
//        {
//        }

//        public DefaultQuestionsController(ControllerBase other) : base(other)
//        {
//        }

//        protected override IQueryable<DefaultQuestion> SetQueryAttributes(IQueryable<DefaultQuestion> query, Actions action)
//        {
//            return base.SetQueryAttributes(query, action);
//        }

//        protected override Task<DefaultQuestion> BeforeActionAsync(DefaultQuestion entity, Actions action)
//        {
//            return base.BeforeActionAsync(entity, action);
//        }

//        protected override Task<DefaultQuestion> AfterActionAsync(DefaultQuestion entity, Actions action)
//        {
//            return base.AfterActionAsync(entity, action);
//        }
//    }
//}