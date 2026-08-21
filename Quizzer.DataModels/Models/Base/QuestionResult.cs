using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Speichert das Ergebnis eines Spielers für eine einzelne Frage in einem Spiel.
    /// Der Unique-Index stellt sicher, dass ein Spieler pro Frage und Spielfeld-Zelle
    /// nur ein Ergebnis haben kann. Gespeichert in der Tabelle <c>question.QuestionResult</c>.
    /// </summary>
    [Table(nameof(QuestionResult), Schema = "question")]
    [Index(nameof(PlayerId), nameof(QuestionBaseId), nameof(GameId), nameof(GameGridCoordinateId), IsUnique = true)]
    public class QuestionResult : ModelBase<QuestionResult>
    {
        /// <summary>Fremdschlüssel zum Spieler, dem das Ergebnis gehört.</summary>
        public Guid PlayerId { get; set; }

        /// <summary>Fremdschlüssel zur beantworteten Frage.</summary>
        public Guid QuestionBaseId { get; set; }

        /// <summary>Fremdschlüssel zum zugehörigen Spiel.</summary>
        public Guid GameId { get; set; }

        /// <summary>Fremdschlüssel zur Spielfeld-Zelle, in der die Frage gespielt wurde.</summary>
        public Guid GameGridCoordinateId { get; set; }

        /// <summary>Gibt an, ob der Spieler die Frage korrekt beantwortet hat.</summary>
        public bool CorrectAnswered { get; set; }

        /// <summary>Anzahl der richtigen Teilantworten (relevant für Mehrschritt-Fragen).</summary>
        public int RightCount { get; set; }

        /// <summary>Anzahl der falschen Teilantworten.</summary>
        public int WrongCount { get; set; }

        /// <summary>Anzahl der nachträglichen Korrekturen durch den Spielleiter.</summary>
        public int CorrectionsCount { get; set; }

        /// <summary>Erzielte Punkte (positiv).</summary>
        public int Score { get; set; }

        /// <summary>Abgezogene Punkte (Minuspunkte bei falscher Antwort).</summary>
        public int MinusScore { get; set; }

        /// <summary>Manuelle Korrektur des Spielleiters (kann positiv oder negativ sein).</summary>
        public int Correction { get; set; }

        /// <summary>Endgültiger Punktestand: <c>Score + Correction - MinusScore</c>.</summary>
        public int FinalScore => Score + Correction - MinusScore;

        /// <summary>Navigation-Property zur beantworteten Frage.</summary>
        public QuestionBase QuestionBase { get; set; } = null!;

        /// <summary>Navigation-Property zum zugehörigen Spiel.</summary>
        public Game Game { get; set; } = null!;

        /// <summary>Navigation-Property zum Spieler.</summary>
        public Player Player { get; set; } = null!;

        /// <summary>Navigation-Property zur Spielfeld-Zelle.</summary>
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