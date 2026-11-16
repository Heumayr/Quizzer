using LocalBuzzer.Service.Base.States.Minispiele;

namespace LocalBuzzer.Service.Base
{
    /// <summary>Der Spielleiter im Minispiel (M35 B3, E1): Runde starten, wiederholen, schliessen, abbrechen.</summary>
    public partial class BuzzerController
    {
        /// <summary>Startet die naechste Runde; <c>false</c>, wenn keine aussteht oder eine laeuft.</summary>
        public async Task<bool> StartMinigameRoundAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            if (!StateManager.MinigameState.StartNextRound())
                return false;

            await SendStateAsync(ct);
            return true;
        }

        /// <summary>Spielt die laufende oder zuletzt gespielte Runde neu - ihre Abgaben zaehlen nicht.</summary>
        public async Task<bool> RepeatMinigameRoundAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            if (!StateManager.MinigameState.RepeatRound())
                return false;

            await SendStateAsync(ct);
            return true;
        }

        /// <summary>Schliesst die laufende Runde, auch wenn ein Telefon fehlt.</summary>
        public Task CloseMinigameRoundAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            return MeldeUndSendeAsync(StateManager.MinigameState.CloseRound(), ct);
        }

        /// <summary>Bricht das Minispiel ab; die laufende Runde zaehlt nicht.</summary>
        public Task AbortMinigameAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            return MeldeUndSendeAsync(StateManager.MinigameState.Abort(), ct);
        }

        private async Task MeldeUndSendeAsync(MinigameRoundClosed? geschlossen, CancellationToken ct)
        {
            if (geschlossen != null)
                EventBus.OnMinigameRoundClosed(geschlossen);

            await SendStateAsync(ct);
        }
    }
}
