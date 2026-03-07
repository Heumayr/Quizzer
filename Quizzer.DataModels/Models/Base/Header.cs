using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Header), Schema = "base")]
    public class Header : ModelBase
    {
        public Guid GameId { get; set; }

        public HeaderType HeaderType { get; set; }
        public int Index { get; set; } = 0;

        public Game? Game { get; set; }
    }
}