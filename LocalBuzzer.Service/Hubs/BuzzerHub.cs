using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace LocalBuzzer.Service.Hubs
{
    public sealed class BuzzerHub : Hub
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

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // Nur die aktuelle Verbindung des Spielers darf ihn auf getrennt setzen - eine ersetzte,
            // die erst jetzt zu Ende geht, wuerde sonst das neue Geraet als weg melden.
            if (_registry.Unregister(Context.ConnectionId, out var player))
                player.ConnectionState = PlayerConnection.Disconnected;

            return base.OnDisconnectedAsync(exception);
        }

        public async Task Buzz()
        {
            if (_stateManager.CurrentState is not BuzzerState buzz)
                return;

            // M33: nach Ablauf des Countdowns zählt kein Buzzer mehr, bis der Spielleiter freigibt.
            if (_stateManager.ZeitAbgelaufen)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            if (buzz.TryBuzz(player))
            {
                _bus.OnWinner(player, _stateManager.Round);
                await Clients.All.SendAsync("StateChanged", _stateManager.CreateClientState());
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

            // Wer abgibt, sagt die Verbindung - nicht die Nachricht. Ein Spieler kann so nur
            // fuer sich selbst abgeben, egal was er mitschickt.
            result.PlayerId = player.Id;
            result.Player = player;
            inputState.SetInput(result);
            _bus.OnPlayerSubmittedInput(result);

            await Clients.All.SendAsync("StateChanged", _stateManager.CreateClientState());

            if (inputState.Locked)
            {
                _stateManager.LockAll();
                _bus.OnAllPlayersSubmittedInput(inputState.InputsForPlayer);
                await Clients.All.SendAsync("StateChanged", _stateManager.CreateClientState());
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

            results.PlayerId = player.Id;
            results.Player = player;
            keySelector.SetSelectedKeys(results);

            // Nur Gepruefte weitermelden - eine verworfene Abgabe (doppelte oder unbekannte
            // Tasten) erreicht den Spielleiter nicht.
            if (!results.CommittedResult)
                return;

            _bus.OnPlayerSelectedKeys(results);

            if (keySelector.Locked)
            {
                _stateManager.LockAll();
                _bus.OnAllPlayersSelectedKeys(keySelector.KeyResultsForPlayer);
                await Clients.All.SendAsync("StateChanged", _stateManager.CreateClientState());
            }
        }
    }
}
