using Quizzer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public class GameGridCoordinate
    {
        public GameGridCoordinate(int y, int x)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public int Phase { get; set; } = 1;

        public Guid QuestionsId { get; set; }

        public bool IsDone { get; set; }

        public string DisplayBuild => $"{X}/{Y}: {Question?.Designation}";
        public string DisplayPlay => $"{Question?.Points}";

        [ClearOnSave]
        public QuestionBase? Question { get; set; }
    }
}