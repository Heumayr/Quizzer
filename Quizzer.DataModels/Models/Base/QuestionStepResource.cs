using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(QuestionStepResource), Schema = "question")]
    public class QuestionStepResource : ModelBase
    {
        public Guid QuestionBaseId { get; set; }

        public string GroupKey { get; set; } = string.Empty;

        public bool IsResult { get; set; } = false;

        public bool IsFinish { get; set; } = false;

        public int SequenceNumber { get; set; }

        public string StepText { get; set; } = string.Empty;

        public FinishType FinishType { get; set; } = FinishType.AllPreviousSteps;

        public string ResourceFileName { get; set; } = string.Empty;

        public ResourceType ResourceTyp { get; set; } = ResourceType.None;

        [InverseProperty(nameof(StepXStep.From))]
        public List<StepXStep> Froms { get; set; } = new();

        [InverseProperty(nameof(StepXStep.To))]
        public List<StepXStep> Tos { get; set; } = new();

        [NotMapped]
        public bool HasResource => ResourceTyp != ResourceType.None;

        [NotMapped]
        public string QuestionViewKey { get; set; } = string.Empty;

        [NotMapped]
        public bool IsStart { get; set; }

        public override string ToString()
        {
            return $"{SequenceNumber}-{Designation}";
        }
    }
}