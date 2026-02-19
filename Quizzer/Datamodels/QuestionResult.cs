using Quizzer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public class QuestionResult : ModelBase
    {
        public Guid PlayerId { get; set; }

        public Guid QuestionId { get; set; }

        public Guid GameId { get; set; }

        public bool CorrectAnswered { get; set; }

        public int Score { get; set; }

        public int MinusScore { get; set; }

        public int FinalScore => Score - MinusScore;

        [ClearOnSave]
        public QuestionBase? Question { get; set; }

        [ClearOnSave]
        public Game? Game { get; set; }

        [ClearOnSave]
        public Player? Player { get; set; }
    }
}