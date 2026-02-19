using Quizzer.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Quizzer.Datamodels
{
    public class Game : ModelBase
    {
        public int Height { get; set; } = 10;

        public int Width { get; set; } = 10;

        public int Depth { get; set; } = 0;

        public List<Guid> PlayerIds { get; set; } = new();

        public ObservableCollection<GameGridCoordinate> GameGridCoordinates { get; set; } = new();

        public List<QuestionResult> QuestionResults { get; set; } = new();

        [ClearOnSave]
        public List<Player> Players { get; set; } = new();
    }
}