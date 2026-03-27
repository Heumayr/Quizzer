using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public sealed class LayoutStateManager
    {
        public List<IBuzzerLayoutState> States { get; set; } = new();

        public BuzzerControlsLayout CurrentLayout
        {
            get => field;
            private set
            {
                field = value;
            }
        } = BuzzerControlsLayout.None;

        public int Round { get; set; }

        public IBuzzerLayoutState? CurrentState { get; private set; }

        public BuzzerState BuzzerState { get; private set; }
        public BuzzerKeySelector BuzzerKeySelector { get; private set; }

        private readonly GameAccessor _gameAccessor;

        public LayoutStateManager(GameAccessor gameAccessor)
        {
            BuzzerState = new(gameAccessor);
            BuzzerKeySelector = new(gameAccessor);

            States.Add(BuzzerState);
            States.Add(BuzzerKeySelector);

            _gameAccessor = gameAccessor;
        }

        public void ResetLayouts(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None)
        {
            CurrentLayout = layout;
            Round = round;

            //only one must be active!
            CurrentState = States.SingleOrDefault(s => s.BuzzerControlsLayout == layout);

            foreach (var state in States)
            {
                if (state.BuzzerControlsLayout == CurrentLayout)
                    state.Reset();
                else
                    state.ClearAndLock();
            }
        }

        public void LockAll()
        {
            CurrentState?.LockAll();

            foreach (var state in States)
            {
                if (state == CurrentState) continue;

                state.LockAll();
            }
        }
    }
}