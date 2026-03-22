using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(QuestionResult), Schema = "question")]
    [Index(nameof(PlayerId), nameof(QuestionBaseId), nameof(GameId), nameof(GameGridCoordinateId), IsUnique = true)]
    public class QuestionResult : ModelBase
    {
        public Guid PlayerId { get; set; }

        public Guid QuestionBaseId { get; set; }

        public Guid GameId { get; set; }

        public Guid GameGridCoordinateId { get; set; }

        public bool CorrectAnswered { get; set; }

        public int Score { get; set; }

        public int MinusScore { get; set; }

        public int FinalScore => Score - MinusScore;

        public QuestionBase QuestionBase { get; set; } = null!;

        public Game Game { get; set; } = null!;

        public Player Player { get; set; } = null!;

        public GameGridCoordinate GameGridCoordinate { get; set; } = null!;
    }
}