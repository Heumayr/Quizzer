using Newtonsoft.Json;
using Quizzer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public class Category : ModelBase
    {
        [ClearOnSave]
        public List<QuestionBase> Questions { get; set; } = new List<QuestionBase>();
    }
}