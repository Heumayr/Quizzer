using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;

namespace LocalBuzzer.Service.Base
{
    public sealed class LayoutStateManager
    {
        public List<IBuzzerLayoutState> States { get; } = new();

        public BuzzerControlsLayout CurrentLayout { get; private set; } = BuzzerControlsLayout.None;
        public int Round { get; private set; }
        public IBuzzerLayoutState? CurrentState { get; private set; }

        public bool AllLocked { get; private set; }

        public BuzzerState BuzzerState { get; }
        public BuzzerKeySelector BuzzerKeySelector { get; }

        private readonly GameAccessor _gameAccessor;

        public LayoutStateManager(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;

            BuzzerState = new(gameAccessor);
            BuzzerKeySelector = new(gameAccessor);

            States.Add(BuzzerState);
            States.Add(BuzzerKeySelector);
        }

        public void ResetLayouts(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None)
        {
            CurrentLayout = layout;
            Round = round;
            AllLocked = false;

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

            AllLocked = true;
        }

        public ClientLayoutStateDto CreateClientState(Player? player = null)
        {
            return new ClientLayoutStateDto
            {
                PlayerName = player?.CalculatedDisplayName,
                Round = Round,
                Layout = CurrentLayout,
                CurrentLayoutLocked = CurrentState?.Locked ?? true,
                AllLocked = AllLocked,
                LayoutInfo = CurrentState?.BuzzerStateInfo,
                Winner = BuzzerState.Winner?.CalculatedDisplayName
            };
        }
    }
}