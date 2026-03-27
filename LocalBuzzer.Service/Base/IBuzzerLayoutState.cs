using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public interface IBuzzerLayoutState
    {
        public BuzzerControlsLayout BuzzerControlsLayout { get; }

        public void Reset();

        public void ClearAndLock();

        public bool Locked { get; set; }

        public object BuzzerStateInfo { get; }

        public void LockAll();
    }
}