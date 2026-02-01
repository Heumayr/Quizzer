using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public abstract class ModelBase
    {
        public Guid Id { get; set; }

        public string Designation { get; set; } = string.Empty;
    }
}