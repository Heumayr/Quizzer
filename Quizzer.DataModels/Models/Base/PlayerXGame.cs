using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Verknüpfungstabelle zwischen <see cref="Player"/> und <see cref="Game"/> (n:m-Beziehung).
    /// Stellt sicher, dass jeder Spieler pro Spiel nur einmal eingetragen ist (Unique-Index).
    /// Gespeichert in der Tabelle <c>base.PlayerXGame</c>.
    /// </summary>
    [Table(nameof(PlayerXGame), Schema = "base")]
    [Index(nameof(PlayerId), nameof(GameId), IsUnique = true)]
    public class PlayerXGame : ModelBase<PlayerXGame>
    {
        /// <summary>Fremdschlüssel zum Spieler.</summary>
        public Guid PlayerId { get; set; }

        /// <summary>Fremdschlüssel zum Spiel.</summary>
        public Guid GameId { get; set; }

        /// <summary>Navigation-Property zum verknüpften Spieler.</summary>
        public Player Player { get; set; } = null!;

        /// <summary>Navigation-Property zum verknüpften Spiel.</summary>
        public Game Game { get; set; } = null!;

        public override PlayerXGame CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new PlayerXGame
            {
                PlayerId = PlayerId,
                GameId = GameId,
                Player = null!,
                Game = null!
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}