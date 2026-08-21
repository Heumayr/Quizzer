using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Collections.Concurrent;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace LocalBuzzer.Service.Hubs
{
    public sealed class BuzzerHub : Hub
    {
        private static int _counter;
        private static readonly ConcurrentDictionary<string, Player> PlayerByConn = new();

        private readonly LayoutStateManager _stateManager;
        private readonly BuzzerEventBus _bus;
        private readonly GameAccessor _gameAccessor;

        public BuzzerHub(LayoutStateManager stateManager, BuzzerEventBus bus, GameAccessor gameAccessor)
        {
            _stateManager = stateManager;
            _bus = bus;
            _gameAccessor = gameAccessor;
        }

        public override async Task OnConnectedAsync()
        {
            var game = _gameAccessor.GetGame?.Invoke();
            if (game == null)
                throw new HubException("Kein Spiel aktiv.");

            var qs = Context.GetHttpContext()?.Request.Query["playerid"].ToString();

            if (!Guid.TryParse(qs, out var guid))
                throw new HubException("Invalid or missing player id.");

            var player = game.Players.FirstOrDefault(p => p.Id == guid);
            if (player == null)
                throw new HubException("Player not found.");

            if (player.ConnectionState == PlayerConnection.Connected)
                throw new HubException($"{player.DisplayName} bereits verbunden.");

            player.ConnectionState = PlayerConnection.Connected;
            PlayerByConn[Context.ConnectionId] = player;
            Interlocked.Increment(ref _counter);

            _bus.OnAssigned(player.CalculatedDisplayName, player.Id);

            await Clients.Caller.SendAsync("Assigned", _stateManager.CreateClientState(player));
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            if (PlayerByConn.TryRemove(Context.ConnectionId, out var player) && player != null)
            {
                player.ConnectionState = PlayerConnection.Disconnected;
                Interlocked.Decrement(ref _counter);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public async Task Buzz()
        {
            if (_stateManager.CurrentState is not BuzzerState buzz)
                return;

            if (!PlayerByConn.TryGetValue(Context.ConnectionId, out var player) || player == null)
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

            if (!PlayerByConn.TryGetValue(Context.ConnectionId, out var player) || player == null)
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

            if (!PlayerByConn.TryGetValue(Context.ConnectionId, out var player) || player == null)
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