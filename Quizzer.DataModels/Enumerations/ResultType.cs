using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    public enum ResultType
    {
        None = 0,
        OnlyResult = 1,
        AllPreviousSteps = 2,
        ResultReferencer = 3,
    }
}