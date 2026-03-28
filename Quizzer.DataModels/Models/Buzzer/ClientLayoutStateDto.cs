using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Models.Buzzer
{
    public sealed class ClientLayoutStateDto
    {
        public string? PlayerName { get; set; }
        public int Round { get; set; }
        public BuzzerControlsLayout Layout { get; set; }
        public bool CurrentLayoutLocked { get; set; }
        public bool AllLocked { get; set; }
        public object? LayoutInfo { get; set; }
        public string? Winner { get; set; }
    }
}