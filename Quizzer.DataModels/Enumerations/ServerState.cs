using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    [Flags]
    public enum ServerState
    {
        None = 0,
        Stopped = 1 << 0,
        Starting = 1 << 1,
        Running = 1 << 2,
        AllConnected = 1 << 3,
        Stopping = 1 << 4,
        Error = 1 << 5,

        ActiveState = Running | AllConnected
    }
}