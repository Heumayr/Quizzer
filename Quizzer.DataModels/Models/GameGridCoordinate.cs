using Quizzer.DataModels.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Quizzer.DataModels.Models
{
    public class GameGridCoordinate
    {
        private QuestionBase? question;

        public GameGridCoordinate(int y, int x)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public int Phase { get; set; } = 1;

        public Guid QuestionId { get; set; }

        public bool IsDone { get; set; }

        public int CurrentPoints { get; set; }

        public int CurrentMinusPoints { get; set; }

        public void CalculatedPoints()
        {
            if (IsDone) return;

            if (Question == null || Game == null)
            {
                CurrentPoints = 0;
                CurrentMinusPoints = 0;
                return;
            }

            var calculated = Question.Points * (Game.DifficultyMultiplier * (int)Question.Difficulty + 1) + (Game.DifficultyAddition * (int)Question.Difficulty);
            calculated = calculated * (Game.PhaseMultiplier * Phase) + (Game.PhaseAddition * Phase);
            CurrentPoints = (int)calculated;

            var calculatedMinus = Question.MinusPoints * (Game.DifficultyMinusMultiplier * (int)Question.Difficulty + 1) + (Game.DifficultyMinusAddition * (int)Question.Difficulty);
            calculatedMinus = calculatedMinus * (Game.PhaseMultiplier * Phase) + (Game.PhaseAddition * Phase);
            CurrentMinusPoints = (int)calculatedMinus;
        }

        public void RaisePhase()
        {
            if (IsDone) return;

            Phase++;
            CalculatedPoints();
        }

        [JsonIgnore]
        public string DisplyCoords => $"{X}/{Y}";

        [JsonIgnore]
        public string DisplayBuild => $"{Question?.Designation}";

        [JsonIgnore]
        public string DisplayPlay => $"{CurrentPoints} pts / -{CurrentMinusPoints} pts";

        [JsonIgnore]
        public string DisplayMaster => !string.IsNullOrEmpty(Question?.DesignationShort) ? $"{Question?.DesignationShort}" : DisplayBuild;

        [ClearOnSave]
        public QuestionBase? Question
        {
            get => question;
            set
            {
                question = value;
            }
        }

        [ClearOnSave]
        public Game? Game { get; set; }
    }
}