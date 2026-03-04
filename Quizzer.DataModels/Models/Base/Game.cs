using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Game), Schema = "base")]
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

        public List<Header> Columns { get; set; } = new();

        public List<Header> Rows { get; set; } = new();

        public List<GameGridCoordinate> GameGridCoordinates { get; set; } = new();

        public List<QuestionResult> QuestionResults { get; set; } = new();

        public List<PlayerXGame> PlayerXGames { get; set; } = new();

        [NotMapped]
        public Dictionary<int, string> ColumnHeader { get; set; } = new();

        [NotMapped]
        public Dictionary<int, string> RowHeader { get; set; } = new();

        [NotMapped]
        public IEnumerable<Player> Players => PlayerXGames.Select(x => x.Player);
    }
}