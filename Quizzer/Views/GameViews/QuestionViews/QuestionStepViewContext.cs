using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Views.GameViews.QuestionViews
{
    public class QuestionStepViewContext
    {
        public CurrentQuestionViewModel Owner { get; set; } = null!;
        public QuestionStepResource? Step { get; set; }

        public QuestionBase? Question => Owner.Question;

        public QuestionStepResource[] AllSteps => Owner.QuestionSteps;

        public QuestionStepResource[] PreviousSteps =>
            Step == null
                ? []
                : Owner.QuestionSteps
                    .Where(s => s.SequenceNumber < Step.SequenceNumber)
                    .OrderBy(s => s.SequenceNumber)
                    .ToArray();

        public QuestionStepResource[] NextSteps =>
            Step == null
                ? []
                : Owner.QuestionSteps
                    .Where(s => s.SequenceNumber > Step.SequenceNumber)
                    .OrderBy(s => s.SequenceNumber)
                    .ToArray();

        public QuestionType QuestionType => Question?.Typ ?? default;
    }
}