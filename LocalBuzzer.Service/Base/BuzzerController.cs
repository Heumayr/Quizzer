using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Enumerations;

namespace LocalBuzzer.Service.Base
{
    public partial class BuzzerController : IDisposable
    {
        public BuzzerEventBus EventBus { get; internal set; }
        public LayoutStateManager StateManager { get; internal set; }
        public IHubContext<BuzzerHub> HubContext { get; internal set; }
        public PlayerConnectionRegistry Registry { get; internal set; }

        public BuzzerController(BuzzerEventBus eventBus, IHubContext<BuzzerHub> hubContext, LayoutStateManager stateManager,
            PlayerConnectionRegistry registry)
        {
            EventBus = eventBus;
            HubContext = hubContext;
            StateManager = stateManager;
            Registry = registry;
        }

        /// <summary>Jedem Telefon seinen Zustand (M35 B3) - vorher ging dasselbe an alle.</summary>
        private Task SendStateAsync(CancellationToken ct)
            => ClientStateSender.SendToEachAsync(HubContext.Clients, Registry, StateManager, ct);

        public async Task ResetRoundAsync(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None, CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.ResetLayouts(round, layout);
            EventBus.OnReset(round);

            await SendStateAsync(ct);
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

            // Beim Minispiel schliesst "Runde jetzt schliessen" nur dessen Runde - die naechste kann noch kommen.
            if (zustand is MinigameState)
            {
                await CloseMinigameRoundAsync(ct);
                return;
            }

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

            await SendStateAsync(ct);
        }

        /// <summary>
        /// Der Countdown ist abgelaufen (M33): die Telefone sperren, bis <see cref="GibEingabenFreiAsync"/>
        /// kommt. Ausgewertet wird nichts - dafür bleibt „Runde schließen".
        /// </summary>
        public async Task SperreWegenZeitAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.SperreWegenZeit();

            await SendStateAsync(ct);
        }

        /// <summary>Der Spielleiter erlaubt nach dem Ablauf wieder Eingaben (M33).</summary>
        public async Task GibEingabenFreiAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.GibFrei();

            await SendStateAsync(ct);
        }

        /// <summary>
        /// Nur diese duerfen die gewoehnliche Frage beantworten (M35 C3b); <c>null</c>: alle. Die Telefone der
        /// anderen sind gesperrt und sagen es, auch nach dem Wiederverbinden - mit <paramref name="saetze"/> je Gesperrtem
        /// eigens, etwa wer ihn per Joker gesperrt hat (M35 C7b).
        /// </summary>
        public async Task SetAnswerersAsync(
            IEnumerable<Guid>? antwortende, IReadOnlyDictionary<Guid, string>? saetze = null, CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.AnswerLock.Restrict(antwortende, saetze);

            await SendStateAsync(ct);
        }

        private void CheckServerRunning()
        {
            if (HubContext is null || EventBus is null || StateManager is null || Registry is null)
                throw new InvalidOperationException("Server is not running.");
        }

        public void Dispose()
        {
            StoppeFristwache();
            HubContext = null!;
            EventBus = null!;
            StateManager = null!;
            Registry = null!;
        }
    }
}