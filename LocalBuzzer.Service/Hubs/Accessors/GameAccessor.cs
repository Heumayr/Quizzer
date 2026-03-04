using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Hubs.Accessors
{
    public sealed class GameAccessor
    {
        public Func<Game?>? GetGame { get; set; }
    }
}