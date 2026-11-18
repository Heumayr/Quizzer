using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace LocalBuzzer.Service.Hubs
{
    public sealed partial class BuzzerHub : Hub
    {
        /// <summary>Was ein Telefon zu sehen bekommt, wenn ein anderes Geraet desselben Spielers es abloest.</summary>
        public const string ReplacedMessage = "Dieses Gerät wurde durch ein anderes ersetzt.";

        private readonly LayoutStateManager _stateManager;
        private readonly BuzzerEventBus _bus;
        private readonly GameAccessor _gameAccessor;
        private readonly PlayerConnectionRegistry _registry;
        private readonly ILogger<BuzzerHub> _logger;
        private readonly BuzzerAccessKeys _accessKeys;

        public BuzzerHub(LayoutStateManager stateManager, BuzzerEventBus bus, GameAccessor gameAccessor,
            PlayerConnectionRegistry registry, ILogger<BuzzerHub> logger, BuzzerAccessKeys accessKeys)
        {
            _stateManager = stateManager;
            _bus = bus;
            _gameAccessor = gameAccessor;
            _registry = registry;
            _logger = logger;
            _accessKeys = accessKeys;
        }

        public override async Task OnConnectedAsync()
        {
            var player = ResolvePlayer();
            var replacedConnectionId = _registry.Register(Context.ConnectionId, player);

            try
            {
                if (replacedConnectionId != null)
                    await NotifyReplacedAsync(replacedConnectionId);

                player.ConnectionState = PlayerConnection.Connected;

                _bus.OnAssigned(player.CalculatedDisplayName, player.Id);

                await Clients.Caller.SendAsync("Assigned", _stateManager.CreateClientState(player));
                await base.OnConnectedAsync();
            }
            catch
            {
                // Die Anmeldung kam nicht zu Ende - der Eintrag darf nicht stehen bleiben, sonst
                // gilt der Spieler als verbunden, ohne dass sein Telefon je "Assigned" gesehen hat.
                if (_registry.Unregister(Context.ConnectionId, out _))
                    player.ConnectionState = PlayerConnection.Disconnected;

                throw;
            }
        }

        /// <summary>Loest die Spieler-Kennung aus der Abfrage gegen das geladene Spiel auf.</summary>
        private Player ResolvePlayer()
        {
            var game = _gameAccessor.GetGame?.Invoke();
            if (game == null)
                throw new HubException("Kein Spiel geladen – der Spielleiter muss zuerst ein Spiel öffnen.");

            // Der Ausweis ist der abgeleitete Zugangsschluessel, nicht die Spieler-Id
            // (Sicherheitspruefung 2026-10-21, BuzzerAccessKeys).
            var key = Context.GetHttpContext()?.Request.Query["key"].ToString();

            if (string.IsNullOrWhiteSpace(key))
                throw new HubException("Fehlender Zugangsschlüssel – bitte den QR-Code neu scannen oder sich neu anmelden.");

            var player = _accessKeys.FindeSchluessel(key, game.Players, p => p.Id);
            if (player == null)
                throw new HubException("Dieser Zugang gehört nicht zum laufenden Spiel – bitte den QR-Code dieses Spiels scannen oder sich neu anmelden.");

            return player;
        }

        /// <summary>
        /// Sagt dem bisherigen Geraet des Spielers, dass es abgeloest wurde. Diese Verbindung ist
        /// meist laengst tot (Bildschirm aus, WLAN-Wechsel) - ein Fehler beim Senden darf die
        /// neue Anmeldung nicht scheitern lassen.
        /// </summary>
        private async Task NotifyReplacedAsync(string oldConnectionId)
        {
            try
            {
                await Clients.Client(oldConnectionId).SendAsync("Replaced", ReplacedMessage);
            }
            catch (Exception ex)
            {
                // Bewusst nur protokolliert: das alte Geraet ist nicht mehr erreichbar, und das
                // neue wartet auf "Assigned".
                _logger.LogDebug(ex, "Abgeloeste Verbindung {ConnectionId} war nicht mehr erreichbar.", oldConnectionId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Nur die aktuelle Verbindung des Spielers darf ihn auf getrennt setzen - eine ersetzte,
            // die erst jetzt zu Ende geht, wuerde sonst das neue Geraet als weg melden.
            if (_registry.Unregister(Context.ConnectionId, out var player))
            {
                player.ConnectionState = PlayerConnection.Disconnected;

                // Getrenntes Telefon = keine Abgabe (M35 B3): fehlte nur noch dieses, ist die Runde zu.
                await CloseMinigameRoundIfCompleteAsync();
            }

            await base.OnDisconnectedAsync(exception);
        }

        private Task SendStateToEachAsync() => ClientStateSender.SendToEachAsync(Clients, _registry, _stateManager);

        public async Task Buzz()
        {
            if (_stateManager.CurrentState is not BuzzerState buzz)
                return;

            // M33: nach Ablauf des Countdowns zählt kein Buzzer mehr, bis der Spielleiter freigibt.
            if (_stateManager.ZeitAbgelaufen)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            // M35 C3b: wer diese Frage nicht beantworten darf, gibt nichts ab - auch nichts an den Spielleiter weiter.
            if (!_stateManager.AnswerLock.Allows(player.Id))
                return;

            if (buzz.TryBuzz(player))
            {
                _bus.OnWinner(player, _stateManager.Round);
                await SendStateToEachAsync();
            }
        }

        /// <summary>
        /// Nimmt den Schaetzwert eines Spielers entgegen. Gegenstueck zu
        /// <see cref="SelectionResults"/>; der Browser ruft das aus <c>inputLayout.js</c> auf.
        /// </summary>
        public async Task SubmitInput(InputResult result)
        {
            if (_stateManager.CurrentState is not BuzzerInputState inputState || result == null)
                return;

            // Eine geschlossene Runde nimmt nichts mehr an - und meldet nichts mehr weiter.
            // Bis zur Sicherheitspruefung 2026-10-21 lief jede spaete Abgabe ungeprueft an den
            // Spielleiter durch, setzte dessen Bewertungsvorschlaege neu und schickte jedes Mal
            // StateChanged an alle Telefone.
            if (inputState.Locked || _stateManager.ZeitAbgelaufen)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            // M35 C3b: wer diese Frage nicht beantworten darf, gibt nichts ab - auch nichts an den Spielleiter weiter.
            if (!_stateManager.AnswerLock.Allows(player.Id))
                return;

            // Wer abgibt, sagt die Verbindung - nicht die Nachricht. Ein Spieler kann so nur
            // fuer sich selbst abgeben, egal was er mitschickt.
            result.PlayerId = player.Id;
            result.Player = player;

            // Verworfen (Finale: zweite Abgabe, M37 E4; für eine schon zurückgesetzte Runde, P10-4) erreicht nichts den
            // Spielleiter - gekuerzt ist der Wert hier schon.
            var geschlossen = false;
            if (!_stateManager.TrySubmit(result.ResetCount, () => inputState.SetInput(result, out geschlossen), out var runde))
                return;

            _bus.OnPlayerSubmittedInput(result);

            await SendStateToEachAsync();

            // Nur wer geschlossen hat, löst auf - nach dem Senden war Locked auch für den vorletzten Tipp wahr (M37 P6, R2-4).
            // Und nur dieselbe Runde: kam beim Senden „Runde zurücksetzen“, bleibt die neue offen (P10-3).
            if (geschlossen && _stateManager.LockRound(runde))
            {
                _bus.OnAllPlayersSubmittedInput(inputState.InputsForPlayer, runde); // P11-3: die Marke, gegen ein Zurücksetzen vor dem Lesen
                await SendStateToEachAsync();
            }
        }

        public async Task SelectionResults(SelectionResult results)
        {
            if (_stateManager.CurrentState is not BuzzerKeySelector keySelector || results?.SelectedKeys == null)
                return;

            // Siehe SubmitInput: nach dem Schliessen der Runde nichts mehr annehmen.
            if (keySelector.Locked || _stateManager.ZeitAbgelaufen)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            // M35 C3b: wer diese Frage nicht beantworten darf, gibt nichts ab - auch nichts an den Spielleiter weiter.
            if (!_stateManager.AnswerLock.Allows(player.Id))
                return;

            // M37 T7: in der Team-Abstimmung geht jede Stimme an das ganze Team - je Telefon höchstens fünf in zehn Sekunden.
            // Verworfen bekommt nur der Aufrufer seinen Stand, darin die Stimme, die zählt (M37 P6, R2-3).
            if (!_stateManager.TeamVote.TryTakeVote(player.Id))
            {
                await ClientStateSender.SendToAsync(Clients, Context.ConnectionId, _registry, _stateManager);
                return;
            }

            results.PlayerId = player.Id;
            results.Player = player;
            var geschlossen = false;
            var angenommen = _stateManager.TrySubmit(results.ResetCount, () =>
            {
                geschlossen = keySelector.SetSelectedKeys(results);
                return results.CommittedResult;
            }, out var runde);

            // Nur Gepruefte weitermelden - eine verworfene Abgabe (doppelte oder unbekannte
            // Tasten, geschlossene oder schon zurückgesetzte Runde - P10-4) erreicht den Spielleiter nicht.
            if (!angenommen)
                return;

            _bus.OnPlayerSelectedKeys(results);

            // Wie beim Tipp: nur dieselbe Runde schließen (P10-3).
            if (geschlossen && _stateManager.LockRound(runde))
            {
                _bus.OnAllPlayersSelectedKeys(keySelector.KeyResultsForPlayer, runde); // P11-3
                await SendStateToEachAsync();
            }
            else if (_stateManager.TeamVote.Active)
            {
                // „2 von 3 haben abgestimmt" - nur am eigenen Team.
                await ClientStateSender.SendToTeamAsync(Clients, _registry, _stateManager, player.Id);
            }
        }
    }
}
