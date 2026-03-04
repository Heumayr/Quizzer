using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(QuestionResult), Schema = "base")]
    public class QuestionResult : ModelBase
    {
        public Guid PlayerId { get; set; }

        public Guid QuestionBaseId { get; set; }

        public Guid GameId { get; set; }

        public bool CorrectAnswered { get; set; }

        public int Score { get; set; }

        public int MinusScore { get; set; }

        public int FinalScore => Score - MinusScore;

        public QuestionBase QuestionBase { get; set; } = null!;

        public Game Game { get; set; } = null!;

        public Player Player { get; set; } = null!;
    }
}