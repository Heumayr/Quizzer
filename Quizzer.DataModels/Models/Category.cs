using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Models
{
    public class Category : ModelBase
    {
        [ClearOnSave]
        public List<QuestionBase> Questions { get; set; } = new List<QuestionBase>();
    }
}