using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Quizzer.DataModels.Models
{
    public abstract class ModelBase
    {
        [Key]
        public Guid Id { get; set; }

        public string Designation { get; set; } = string.Empty;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}