using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Game), Schema = "base")]
    public class Game : ModelBase<Game>
    {
        public bool Restart { get; set; } = false;

        public GameState State { get; set; } = GameState.Building;

        public int Height { get; set; } = 10;

        public int Width { get; set; } = 10;

        public int Depth { get; set; } = 0;

        public double CellHeight { get; set; } = 100;

        public double CellWidth { get; set; } = 120;

        public double DifficultyMultiplier { get; set; } = 1;

        public int DifficultyAddition { get; set; } = 0;

        public double DifficultyMinusMultiplier { get; set; } = 0.5;

        public int DifficultyMinusAddition { get; set; } = 0;

        public double PhaseMultiplier { get; set; } = 1;

        public int PhaseAddition { get; set; }

        public int CurrentRound { get; set; } = 1;

        public int Phase { get; set; } = 1;

        public void RaisePhase()
        {
            Phase++;
            SetPhaseAndSetCoordinatesPhase(Phase);
        }

        public void LowerPhase()
        {
            Phase--;
            SetPhaseAndSetCoordinatesPhase(Phase);
        }

        public void SetPhaseAndSetCoordinatesPhase(int phase)
        {
            if (phase < 1) phase = 1;

            Phase = phase;

            if (GameGridCoordinates.Count == 0) return;

            foreach (var coord in GameGridCoordinates)
            {
                coord.SetPhase(phase);
            }
        }

        public List<Header> Headers { get; set; } = new();

        public List<GameGridCoordinate> GameGridCoordinates { get; set; } = new();

        [NotMapped]
        public IEnumerable<QuestionResult> QuestionResults => GameGridCoordinates.SelectMany(x => x.QuestionResults);

        public List<PlayerXGame> PlayerXGames { get; set; } = new();

        [NotMapped]
        public IEnumerable<Header> Columns => Headers.Where(h => h.HeaderType == HeaderType.Column);

        [NotMapped]
        public IEnumerable<Header> Rows => Headers.Where(h => h.HeaderType == HeaderType.Row);

        [NotMapped]
        public IEnumerable<Player> Players => PlayerXGames.Select(x => x.Player);

        public void CalculateAndSetCurrentPoints()
        {
            if (GameGridCoordinates.Count == 0) return;

            foreach (var coord in GameGridCoordinates)
            {
                coord.CalculateAndSetCurrentPoints();
            }
        }

        public override Game CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new Game
            {
                Restart = Restart,
                State = State,
                Height = Height,
                Width = Width,
                Depth = Depth,
                CellHeight = CellHeight,
                CellWidth = CellWidth,
                DifficultyMultiplier = DifficultyMultiplier,
                DifficultyAddition = DifficultyAddition,
                DifficultyMinusMultiplier = DifficultyMinusMultiplier,
                DifficultyMinusAddition = DifficultyMinusAddition,
                PhaseMultiplier = PhaseMultiplier,
                PhaseAddition = PhaseAddition,
                CurrentRound = CurrentRound,
                Phase = Phase,
                Headers = new List<Header>(),
                GameGridCoordinates = new List<GameGridCoordinate>(),
                PlayerXGames = new List<PlayerXGame>()
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}