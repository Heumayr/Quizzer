using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;

namespace LocalBuzzer.Service.Hubs
{
    /// <summary>Der eine Hub-Eingang der Minispiele (M35 B3): <c>SpielAktion</c> mit Art und Nutzlast.</summary>
    public sealed partial class BuzzerHub
    {
        /// <summary>
        /// Nimmt eine Abgabe im Minispiel an. Geprueft wird am aktiven Zustand
        /// (<see cref="MinigameState.Submit"/>); wer abgibt, sagt die Verbindung. Eine verworfene
        /// Abgabe erreicht den Spielleiter nicht - das Telefon bekommt nur seinen Stand zurueck.
        /// </summary>
        public async Task SpielAktion(MinigameAction? aktion)
        {
            if (_stateManager.CurrentState is not MinigameState spiel || aktion == null)
                return;

            if (_stateManager.ZeitAbgelaufen)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            var ergebnis = spiel.Submit(player, aktion);

            if (ergebnis.Accepted)
                _bus.OnMinigameSubmitted(new MinigameSubmission(player, spiel.Round, ergebnis.Value!));

            // Eine Spielerwahl oeffnet die naechste Stufe - jedes Telefon sieht danach etwas anderes (M35 C2).
            if (ergebnis.Restarted)
            {
                await SendStateToEachAsync();
                return;
            }

            if (ergebnis.Closed != null)
            {
                _bus.OnMinigameRoundClosed(ergebnis.Closed);
                await SendStateToEachAsync();
                return;
            }

            // Die anderen sehen nichts Neues - und ein Update mitten in ihrer Wartezeit stoert nur.
            await ClientStateSender.SendToAsync(Clients, Context.ConnectionId, _registry, _stateManager);
        }

        private async Task CloseMinigameRoundIfCompleteAsync()
        {
            if (_stateManager.CurrentState is not MinigameState spiel)
                return;

            var geschlossen = spiel.CloseIfComplete();

            if (geschlossen == null)
                return;

            _bus.OnMinigameRoundClosed(geschlossen);
            await SendStateToEachAsync();
        }
    }
}
