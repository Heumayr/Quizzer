using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Helpers
{
    public static class Factory
    {
        public static QuestionBase CreateNewQuestion(QuestionType type)
        {
            return type switch
            {
                QuestionType.MultipleChoice => new MultipleChoiceQuestion(),
                _ => new DefaultQuestion()
            };
        }
    }
}