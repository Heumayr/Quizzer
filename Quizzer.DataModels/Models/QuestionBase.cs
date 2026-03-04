using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models
{
    [Table(nameof(QuestionBase), Schema = "base")]
    public class QuestionBase : ModelBase
    {
        public string DesignationShort { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }

        public virtual int Points { get; set; }

        public virtual int MinusPoints { get; set; }

        public string Notes { get; set; } = string.Empty;

        //public abstract QuestionTyp Typ { get; protected set; }

        public Difficulty Difficulty { get; set; } = Difficulty.Level1;

        public List<QuestionStepResource> Steps { get; set; } = new List<QuestionStepResource>();

        public Category? Category { get; set; }
    }
}