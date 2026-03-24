using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(PlayerXGame), Schema = "base")]
    [Index(nameof(PlayerId), nameof(GameId), IsUnique = true)]
    public class PlayerXGame : ModelBase<PlayerXGame>
    {
        public Guid PlayerId { get; set; }
        public Guid GameId { get; set; }

        public Player Player { get; set; } = null!;
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