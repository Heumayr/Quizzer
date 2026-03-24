using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(StepXStep), Schema = "question")]
    [Index(nameof(FromId), nameof(ToId), IsUnique = true)]
    public class StepXStep : ModelBase<StepXStep>
    {
        public Guid? FromId { get; set; }
        public Guid? ToId { get; set; }

        [ForeignKey(nameof(FromId))]
        public QuestionStepResource? From { get; set; }

        [ForeignKey(nameof(ToId))]
        public QuestionStepResource? To { get; set; }

        public override StepXStep CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new StepXStep
            {
                FromId = FromId,
                ToId = ToId,
                From = null,
                To = null
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}