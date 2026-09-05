using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(GameGridCoordinate), Schema = "base")]
    public class GameGridCoordinate : ModelBase<GameGridCoordinate>
    {
        private QuestionBase? question;

        //public GameGridCoordinate(int y, int x)
        //{
        //    X = x;
        //    Y = y;
        //}

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public int Phase { get; set; } = 1;

        public Guid? QuestionBaseId { get; set; }

        public Guid GameId { get; set; }

        public bool IsDone { get; set; }

        public int CurrentPoints { get; set; }

        public int CurrentMinusPoints { get; set; }

        /// <summary>
        /// Rechnet die Punkte dieser Zelle neu. <see cref="CurrentPoints"/> und
        /// <see cref="CurrentMinusPoints"/> sind gespeicherte Spalten, keine Anzeigewerte - was
        /// hier gesetzt wird, schreibt der naechste Speichervorgang fest.
        /// <para>
        /// Deshalb wird nur genullt, wenn wirklich keine Frage zugewiesen ist. Fehlen bloss die
        /// Rueckverweise im Speicher - etwa weil das Spiel geladen wurde, bevor der Rasteraufbau
        /// sie gesetzt hat -, bleibt der bestehende Wert stehen. Bis 2026-09-06 nullte diese
        /// Stelle auch dann, und aus einer Zelle mit 600 Punkten wurde still eine mit null.
        /// </para>
        /// </summary>
        public void CalculateAndSetCurrentPoints()
        {
            if (IsDone) return;

            // Eine Frage kann ueber die Navigation da sein, ueber die Kennung, oder ueber beides.
            var hatFrage = QuestionBase != null
                        || (QuestionBaseId.HasValue && QuestionBaseId.Value != Guid.Empty);

            if (!hatFrage)
            {
                CurrentPoints = 0;
                CurrentMinusPoints = 0;
                return;
            }

            if (QuestionBase == null || Game == null)
            {
                // Es gibt eine Frage, aber die Rueckverweise fehlen: rechnen ist nicht moeglich,
                // also den gespeicherten Stand nicht anfassen.
                return;
            }

            var calculated = QuestionBase.Points * (Game.DifficultyMultiplier * (int)QuestionBase.Difficulty + 1) + (Game.DifficultyAddition * (int)QuestionBase.Difficulty);
            calculated = calculated * (Game.PhaseMultiplier * Phase) + (Game.PhaseAddition * Phase);
            CurrentPoints = (int)calculated;

            var calculatedMinus = QuestionBase.MinusPoints * (Game.DifficultyMinusMultiplier * (int)QuestionBase.Difficulty + 1) + (Game.DifficultyMinusAddition * (int)QuestionBase.Difficulty);
            calculatedMinus = calculatedMinus * (Game.PhaseMultiplier * Phase) + (Game.PhaseAddition * Phase);
            CurrentMinusPoints = (int)calculatedMinus;
        }

        public void RaisePhase()
        {
            if (IsDone) return;

            Phase++;
            CalculateAndSetCurrentPoints();
        }

        public void LowerPhase()
        {
            if (IsDone) return;

            Phase--;
            if (Phase < 1) Phase = 1;

            CalculateAndSetCurrentPoints();
        }

        public void SetPhase(int phase)
        {
            if (IsDone) return;

            Phase = phase;
            CalculateAndSetCurrentPoints();
        }

        public QuestionBase? QuestionBase
        {
            get => question;
            set
            {
                question = value;
            }
        }

        public List<QuestionResult> QuestionResults { get; set; } = new();

        [ForeignKey(nameof(GameId))]
        public Game Game { get; set; } = null!;

        [NotMapped]
        public string DisplyCoords => $"{X}/{Y}";

        [NotMapped]
        public string DisplayBuild => $"{QuestionBase?.Designation}";

        [NotMapped]
        public string DisplayPlay => $"{CurrentPoints} pts / -{CurrentMinusPoints} pts";

        [NotMapped]
        public string DisplayMaster => !string.IsNullOrEmpty(QuestionBase?.DesignationShort) ? $"{QuestionBase?.DesignationShort}" : DisplayBuild;

        public override GameGridCoordinate CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new GameGridCoordinate
            {
                X = X,
                Y = Y,
                Z = Z,
                Phase = Phase,
                QuestionBaseId = QuestionBaseId,
                GameId = GameId,
                IsDone = IsDone,
                CurrentPoints = CurrentPoints,
                CurrentMinusPoints = CurrentMinusPoints,
                QuestionBase = null,
                QuestionResults = new List<QuestionResult>(),
                Game = null!
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}