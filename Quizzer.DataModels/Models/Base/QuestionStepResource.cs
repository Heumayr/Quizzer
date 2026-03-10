using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(QuestionStepResource), Schema = "base")]
    public class QuestionStepResource : ModelBase
    {
        public Guid QuestionBaseId { get; set; }

        public bool IsResult { get; set; } = false;

        public int SquenceNumber { get; set; }

        public string StepText { get; set; } = string.Empty;

        public string ResourceFileName { get; set; } = string.Empty;

        public ResourceType ResourceTyp { get; set; } = ResourceType.None;

        [NotMapped]
        public bool HasResource => ResourceTyp != ResourceType.None;
    }
}