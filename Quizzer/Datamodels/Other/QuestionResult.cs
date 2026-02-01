using System;
using System.Collections.Generic;
using System.Security.RightsManagement;
using System.Text;

namespace Quizzer.Datamodels.Other
{
    public class QuestionResult
    {
        public PlayerInfo[] Results { get; set; } = Array.Empty<PlayerInfo>();
    }

    public class PlayerInfo
    {
        public Player? Player { get; set; }

        public int QuestionScore { get; set; }
    }
}