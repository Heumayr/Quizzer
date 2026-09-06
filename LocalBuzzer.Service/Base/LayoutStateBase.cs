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

        /// <summary>
        /// Wie oft die Runde zurueckgesetzt wurde, seit der Server laeuft.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> die Telefonseite baut ihr Layout nur neu auf, wenn sich
        /// die Kennzeichnung der Frage aendert. Beim Zuruecksetzen bleibt sie gleich - das
        /// Telefon entsperrte also nur, und ein Spieler, der bei Multiple Choice schon
        /// abgegeben hatte, blieb fuer immer gesperrt: sein Knopf trug weiter "Abgegeben".
        /// </para>
        /// <para>
        /// <b><see cref="Round"/> taugt dafuer nicht</b> - <c>ResetRoundAsync</c> wird mit
        /// derselben Rundennummer gerufen, die Zahl aendert sich beim Zuruecksetzen nicht. Dieser
        /// Zaehler steigt bei jedem Zuruecksetzen und macht die Runde auf der Telefonseite
        /// unterscheidbar.
        /// </para>
        /// </summary>
        public int ResetCount { get; private set; }
        public IBuzzerLayoutState? CurrentState { get; private set; }

        public bool AllLocked { get; private set; }

        public BuzzerState BuzzerState { get; }
        public BuzzerKeySelector BuzzerKeySelector { get; }
        public BuzzerInputState BuzzerInputState { get; }

        private readonly GameAccessor _gameAccessor;

        public LayoutStateManager(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;

            BuzzerState = new(gameAccessor);
            BuzzerKeySelector = new(gameAccessor);
            BuzzerInputState = new(gameAccessor);

            States.Add(BuzzerState);
            States.Add(BuzzerKeySelector);
            States.Add(BuzzerInputState);
        }

        public void ResetLayouts(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None)
        {
            CurrentLayout = layout;
            Round = round;
            AllLocked = false;

            ResetCount++;

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
                ResetCount = ResetCount,
                Layout = CurrentLayout,
                CurrentLayoutLocked = CurrentState?.Locked ?? true,
                AllLocked = AllLocked,
                LayoutInfo = CurrentState?.BuzzerStateInfo,
                Winner = BuzzerState.Winner?.CalculatedDisplayName
            };
        }
    }
}