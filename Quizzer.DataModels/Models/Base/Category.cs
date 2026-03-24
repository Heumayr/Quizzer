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
    public class Category : ModelBase<Category>
    {
        [NotMapped]
        public List<QuestionBase> Questions { get; set; } = new List<QuestionBase>();

        public override Category CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new Category
            {
                Questions = new List<QuestionBase>()
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}