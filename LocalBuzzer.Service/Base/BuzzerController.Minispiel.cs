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

            StarteFristwache();
            await SendStateAsync(ct);
            return true;
        }

        /// <summary>Spielt die laufende oder zuletzt gespielte Runde neu - ihre Abgaben zaehlen nicht.</summary>
        public async Task<bool> RepeatMinigameRoundAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            if (!StateManager.MinigameState.RepeatRound())
                return false;

            StarteFristwache();
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

        /// <summary>
        /// Der Spielleiter legt fest, wer spielt (E1, M35 C2) - wenn der Ausloeser fehlt oder er ueberschreibt;
        /// ohne Partner waehlt danach der Ausloeser am Telefon. <c>false</c> bei einer Art ohne Spielerwahl
        /// oder wenn einer kein Mitspieler ist.
        /// </summary>
        public async Task<bool> SetMinigamePairAsync(Guid chooserId, Guid? partnerId, CancellationToken ct = default)
        {
            CheckServerRunning();

            if (!StateManager.MinigameState.SetPair(chooserId, partnerId))
                return false;

            await SendStateAsync(ct);
            return true;
        }

        private async Task MeldeUndSendeAsync(MinigameRoundClosed? geschlossen, CancellationToken ct)
        {
            if (geschlossen != null)
                EventBus.OnMinigameRoundClosed(geschlossen);

            await SendStateAsync(ct);
        }

        // --- Fristwache (M35 C4): Zocker und Gluecksrad schliessen ihre Runde an der Serveruhr ---

        /// <summary>So oft sieht die Fristwache nach. Entschieden wird an der Serveruhr - der Takt verzoegert nur den Versand.</summary>
        internal static TimeSpan FristTakt { get; } = TimeSpan.FromMilliseconds(50);

        private CancellationTokenSource? fristwache;

        /// <summary>Wacht ueber die laufende Runde, wenn die Art eine Frist hat - eine neue Runde loest die alte Wache ab.</summary>
        private void StarteFristwache()
        {
            StoppeFristwache();

            if (!StateManager.MinigameState.HasDeadline)
                return;

            var abbruch = fristwache = new CancellationTokenSource();
            _ = WacheAsync(StateManager.MinigameState, abbruch.Token);
        }

        private void StoppeFristwache()
        {
            fristwache?.Cancel();
            fristwache?.Dispose();
            fristwache = null;
        }

        /// <summary>
        /// Schliesst die Runde, sobald ihre Frist abgelaufen ist - auch wenn kein Telefon mehr abgibt (alle
        /// getrennt oder niemand steigt aus). Endet mit der Runde, beim Abbruch und beim Zuruecksetzen.
        /// </summary>
        private async Task WacheAsync(MinigameState spiel, CancellationToken ct)
        {
            try
            {
                while (spiel.Phase == MinigamePhase.Running)
                {
                    await Task.Delay(FristTakt, ct);

                    if (spiel.CloseIfDue() is { } geschlossen)
                    {
                        await MeldeUndSendeAsync(geschlossen, ct);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Abgeloest durch eine neue Runde oder den Serverstopp - nichts mehr zu tun.
            }
        }
    }
}
