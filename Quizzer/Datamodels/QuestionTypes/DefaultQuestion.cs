using Quizzer.Datamodels.Enumerations;

using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels.QuestionTypes
{
    public class DefaultQuestion : QuestionBase
    {
        public override int Points { get; set; } = 100;
        public override int MinusPoints { get; set; } = 100;
        public override QuestionTyp Typ { get; protected set; } = QuestionTyp.Default;
    }
}