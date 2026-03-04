using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Header), Schema = "base")]
    public class Header : ModelBase
    {
        [InverseProperty(nameof(Game.Columns))]
        public Guid GameColumnId { get; set; }

        [InverseProperty(nameof(Game.Rows))]
        public Guid GameRowId { get; set; }

        public int Index { get; set; } = 0;
    }
}