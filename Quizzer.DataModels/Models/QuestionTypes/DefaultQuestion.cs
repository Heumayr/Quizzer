using Quizzer.DataModels.Models.Enumerations;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    public class DefaultQuestion : QuestionBase
    {
        public override int Points { get; set; } = 100;
        public override int MinusPoints { get; set; } = 100;
        public override QuestionTyp Typ { get; protected set; } = QuestionTyp.Default;
    }
}