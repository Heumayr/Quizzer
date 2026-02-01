using Quizzer.Datamodels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public class QuestionStepResource
    {
        public bool IsResult { get; set; } = false;

        public int SquenceNumber { get; set; }

        public string StepText { get; set; } = string.Empty;

        public string ResourceFileName { get; set; } = string.Empty;

        public ResourceTyp ResourceTyp { get; set; } = ResourceTyp.None;
    }
}