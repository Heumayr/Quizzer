using Quizzer.DataModels.Enumerations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    [Table(nameof(DefaultQuestion), Schema = "questiontype")]
    public class DefaultQuestion : QuestionBase
    {
        public override int Points { get; set; } = 100;
        public override int MinusPoints { get; set; } = 100;
        //public override QuestionTyp Typ { get; protected set; } = QuestionTyp.Default;
    }
}