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
    public class QuestionResult : ModelBase<QuestionResult>
    {
        public Guid PlayerId { get; set; }

        public Guid QuestionBaseId { get; set; }

        public Guid GameId { get; set; }

        public Guid GameGridCoordinateId { get; set; }

        public bool CorrectAnswered { get; set; }

        public int RightCount { get; set; }
        public int WrongCount { get; set; }
        public int CorrectionsCount { get; set; }

        public int Score { get; set; }

        public int MinusScore { get; set; }

        public int Correction { get; set; }

        public int FinalScore => Score + Correction - MinusScore;

        public QuestionBase QuestionBase { get; set; } = null!;

        public Game Game { get; set; } = null!;

        public Player Player { get; set; } = null!;

        public GameGridCoordinate GameGridCoordinate { get; set; } = null!;

        public override QuestionResult CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new QuestionResult
            {
                PlayerId = PlayerId,
                QuestionBaseId = QuestionBaseId,
                GameId = GameId,
                GameGridCoordinateId = GameGridCoordinateId,
                CorrectAnswered = CorrectAnswered,
                RightCount = RightCount,
                WrongCount = WrongCount,
                CorrectionsCount = CorrectionsCount,
                Score = Score,
                MinusScore = MinusScore,
                Correction = Correction,
                QuestionBase = null!,
                Game = null!,
                Player = null!,
                GameGridCoordinate = null!
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}