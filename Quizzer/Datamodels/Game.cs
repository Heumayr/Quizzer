using Quizzer.Attributes;
using Quizzer.Datamodels.Enumerations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Quizzer.Datamodels
{
    public class Game : ModelBase
    {
        public bool Restart { get; set; } = false;
        public GameState State { get; set; } = GameState.Building;

        public int Height { get; set; } = 10;

        public int Width { get; set; } = 10;

        public int Depth { get; set; } = 0;

        public double CellHeight { get; set; } = 60;

        public double CellWidth { get; set; } = 120;

        public double DifficultyMultiplier { get; set; } = 1;

        public int DifficultyAddition { get; set; } = 0;

        public double DifficultyMinusMultiplier { get; set; } = 1;

        public int DifficultyMinusAddition { get; set; } = 0;

        public double PhaseMultiplier { get; set; } = 1;

        public int PhaseAddition { get; set; }

        public Dictionary<int, string> ColumnHeader { get; set; } = new();

        public Dictionary<int, string> RowHeader { get; set; } = new();

        public List<Guid> PlayerIds { get; set; } = new();

        public ObservableCollection<GameGridCoordinate> GameGridCoordinates { get; set; } = new();

        public List<QuestionResult> QuestionResults { get; set; } = new();

        [ClearOnSave]
        public List<Player> Players { get; set; } = new();
    }
}