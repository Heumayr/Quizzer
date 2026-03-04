using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Category), Schema = "base")]
    public class Category : ModelBase
    {
        [NotMapped]
        public List<QuestionBase> Questions { get; set; } = new List<QuestionBase>();
    }
}