using Quizzer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public class Player : ModelBase
    {
        public int FinalScore => Score - MinusScore;

        public int Score => QuestionResults.Sum(qr => qr.Score);

        public int MinusScore => QuestionResults.Sum(qr => qr.MinusScore);

        public List<QuestionResult> QuestionResults { get; set; } = new();
    }
}