using Quizzer.DataModels.Enumerations;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Ein Joker eines Spielers in einem Spiel (M35 C7, Entwurf F-M35-12). Der Spielleiter vergibt
    /// ihn mit Grund; eingelöst wird er für eine Zelle (C7b). Gespeichert in <c>base.PlayerJoker</c>.
    /// <para>
    /// Löschregeln wie bei <see cref="QuestionResult"/>: am Spieler CASCADE, an Spiel, Zelle und
    /// Ziel NO ACTION - den Riegel je Weg nennt <c>DataContext.OnModelCreating</c>.
    /// </para>
    /// </summary>
    [Table(nameof(PlayerJoker), Schema = "base")]
    public class PlayerJoker : ModelBase<PlayerJoker>
    {
        /// <summary>Längste erlaubte Begründung - die Spalte, der Server und die Maske halten sie.</summary>
        public const int MaxReasonLength = 200;

        /// <summary>Fremdschlüssel zum Spiel, in dem der Joker gilt.</summary>
        public Guid GameId { get; set; }

        /// <summary>Fremdschlüssel zum Spieler, dem der Joker gehört.</summary>
        public Guid PlayerId { get; set; }

        /// <summary>Was der Joker bewirkt.</summary>
        public JokerKind Kind { get; set; } = JokerKind.Double;

        /// <summary>Warum der Spielleiter ihn vergeben hat - höchstens <see cref="MaxReasonLength"/> Zeichen.</summary>
        [MaxLength(MaxReasonLength)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Wann der Spielleiter ihn vergeben hat.</summary>
        public DateTimeOffset GrantedAt { get; set; }

        /// <summary>Die Zelle, für die er eingelöst wurde; <c>null</c> heißt: noch offen.</summary>
        public Guid? GameGridCoordinateId { get; set; }

        /// <summary>Wann er eingelöst wurde.</summary>
        public DateTimeOffset? RedeemedAt { get; set; }

        /// <summary>Bei der Sperre: wen sie trifft.</summary>
        public Guid? TargetPlayerId { get; set; }

        /// <summary>Eingelöst ist er, sobald eines der beiden Einlösefelder steht.</summary>
        [NotMapped]
        public bool IsRedeemed => GameGridCoordinateId != null || RedeemedAt != null;

        public override PlayerJoker CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new PlayerJoker
            {
                GameId = GameId,
                PlayerId = PlayerId,
                Kind = Kind,
                Reason = Reason,
                GrantedAt = GrantedAt,
                GameGridCoordinateId = GameGridCoordinateId,
                RedeemedAt = RedeemedAt,
                TargetPlayerId = TargetPlayerId,
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}
