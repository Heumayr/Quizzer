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

        public BuzzerHub(LayoutStateManager stateManager, BuzzerEventBus bus, GameAccessor gameAccessor,
            PlayerConnectionRegistry registry, ILogger<BuzzerHub> logger)
        {
            _stateManager = stateManager;
            _bus = bus;
            _gameAccessor = gameAccessor;
            _registry = registry;
            _logger = logger;
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

            var qs = Context.GetHttpContext()?.Request.Query["playerid"].ToString();

            if (!Guid.TryParse(qs, out var guid))
                throw new HubException("Ungültige oder fehlende Spieler-Kennung – bitte den QR-Code neu scannen.");

            var player = game.Players.FirstOrDefault(p => p.Id == guid);
            if (player == null)
                throw new HubException("Dieser Spieler gehört nicht zum laufenden Spiel – bitte den QR-Code dieses Spiels scannen.");

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
            if (_stateManager.CurrentState is not BuzzerInputState inputState || result.PlayerId == Guid.Empty)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            // Ein Spieler darf nur fuer sich selbst abgeben.
            if (result.PlayerId != player.Id)
                return;

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
            if (_stateManager.CurrentState is not BuzzerKeySelector keySelector || results.PlayerId == Guid.Empty)
                return;

            if (!_registry.TryGetPlayer(Context.ConnectionId, out var player))
                return;

            if (results.PlayerId != player.Id)
                return;

            results.Player = player;
            keySelector.SetSelectedKeys(results);
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
