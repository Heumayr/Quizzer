using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Enumerations;

namespace LocalBuzzer.Service.Base
{
    public class BuzzerController : IDisposable
    {
        public BuzzerEventBus EventBus { get; internal set; }
        public LayoutStateManager StateManager { get; internal set; }
        public IHubContext<BuzzerHub> HubContext { get; internal set; }

        public BuzzerController(BuzzerEventBus eventBus, IHubContext<BuzzerHub> hubContext, LayoutStateManager stateManager)
        {
            EventBus = eventBus;
            HubContext = hubContext;
            StateManager = stateManager;
        }

        public async Task ResetRoundAsync(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None, CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.ResetLayouts(round, layout);
            EventBus.OnReset(round);

            await HubContext.Clients.All.SendAsync(
                "StateChanged",
                StateManager.CreateClientState(),
                cancellationToken: ct);
        }

        /// <summary>
        /// Schliesst die laufende Runde, ohne auf die noch fehlenden Abgaben zu warten.
        /// <para>
        /// <b>Der Notausgang fuer den Abend.</b> Eine Runde schliesst sonst nur, wenn
        /// <b>restlos jeder</b> Mitspieler abgegeben hat (<c>BuzzerInputState.Locked</c>,
        /// <c>BuzzerKeySelector.Locked</c>). Ein leerer Akku, ein iPhone mit gesperrtem
        /// Bildschirm oder jemand ganz ohne Telefon genuegt, und die Schaetzfrage bekommt nie
        /// ihre Auswertung: kein "am naechsten dran"-Vorschlag, keine Bewertung, und die
        /// uebrigen koennen ihren Tipp weiter aendern, nachdem er vorgelesen wurde.
        /// </para>
        /// <para>
        /// <b>Sperren allein genuegt nicht.</b> Bis 2026-09-07 tat diese Methode nur das - und
        /// hatte im ganzen Programm keinen einzigen Aufrufer. Die Auswertung haengt an den
        /// Sammelereignissen, die sonst nur der Hub ausloest; sie werden hier mit ausgeloest,
        /// mit demselben Inhalt.
        /// </para>
        /// </summary>
        public async Task LockAllAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            var zustand = StateManager.CurrentState;

            StateManager.LockAll();

            switch (zustand)
            {
                case BuzzerInputState eingabe:
                    EventBus.OnAllPlayersSubmittedInput(eingabe.InputsForPlayer);
                    break;

                case BuzzerKeySelector tasten:
                    EventBus.OnAllPlayersSelectedKeys(tasten.KeyResultsForPlayer);
                    break;
            }

            await HubContext.Clients.All.SendAsync(
                "StateChanged",
                StateManager.CreateClientState(),
                cancellationToken: ct);
        }

        private void CheckServerRunning()
        {
            if (HubContext is null || EventBus is null || StateManager is null)
                throw new InvalidOperationException("Server is not running.");
        }

        public void Dispose()
        {
            HubContext = null!;
            EventBus = null!;
            StateManager = null!;
        }
    }
}