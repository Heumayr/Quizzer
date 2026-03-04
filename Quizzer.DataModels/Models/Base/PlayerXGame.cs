using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(PlayerXGame), Schema = "base")]
    [Index(nameof(PlayerId), nameof(GamesId), IsUnique = true)]
    public class PlayerXGame : ModelBase
    {
        public Guid PlayerId { get; set; }
        public Guid GamesId { get; set; }

        public Player Player { get; set; } = null!;
        public Game Game { get; set; } = null!;
    }
}