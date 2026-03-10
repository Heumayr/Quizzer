using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models
{
    [Table(nameof(QuestionBase), Schema = "question")]
    public class QuestionBase : ModelBase
    {
        public string DesignationShort { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }

        public virtual int Points { get; set; }

        public virtual int MinusPoints { get; set; }

        public string Notes { get; set; } = string.Empty;

        public virtual QuestionType Typ { get; protected set; }

        public Difficulty Difficulty { get; set; } = Difficulty.Level1;

        public bool WarnOnResultStep { get; set; } = true;

        public List<QuestionStepResource> Steps { get; set; } = new List<QuestionStepResource>();

        public Category? Category { get; set; }

        [NotMapped]
        public QuestionStepResource[] OrderdSteps => Steps.OrderBy(s => s.SquenceNumber).ToArray() ?? Array.Empty<QuestionStepResource>();

        public QuestionStepResource? GetNextStep(QuestionStepResource? currentStep = null)
        {
            if (!OrderdSteps.Any())
                return null;

            if (currentStep == null)
                return OrderdSteps.First();

            var seq = currentStep.SquenceNumber;

            return OrderdSteps.FirstOrDefault(s => s.SquenceNumber > seq);
        }

        public QuestionStepResource? GetStepBehind(QuestionStepResource? currentStep = null)
        {
            if (!OrderdSteps.Any())
                return null;

            if (currentStep == null)
                return null;

            var reverse = OrderdSteps.Reverse();

            var seq = currentStep.SquenceNumber;

            return reverse.FirstOrDefault(s => s.SquenceNumber < seq);
        }
    }
}