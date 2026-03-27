using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

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

            await Clients.Caller.SendAsync("Assigned",
                player.CalculatedDisplayName, _stateManager.Round, _stateManager.CurrentState?.Locked ?? true, _stateManager.CurrentState?.BuzzerStateInfo);

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            PlayerByConn.TryRemove(Context.ConnectionId, out Player? player);

            if (player != null)
                player.ConnectionState = PlayerConnection.Disconnected;

            Interlocked.Decrement(ref _counter);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task Buzz()
        {
            if (_stateManager.CurrentState is not BuzzerState buzz)
                return;

            PlayerByConn.TryGetValue(Context.ConnectionId, out var player);
            var name = player?.CalculatedDisplayName ?? "Unknown";

            if (buzz.TryBuzz(player))
            {
                _bus.OnWinner(player, _stateManager.Round);
                await Clients.All.SendAsync("BuzzerWinner", name, _stateManager.Round);
            }
        }

        public async Task SelectionResults(SelectionResult results)
        {
            if (_stateManager.CurrentState is not BuzzerKeySelector keySelector || results.PlayerId == Guid.Empty) return;

            PlayerByConn.TryGetValue(Context.ConnectionId, out var player);

            if (results.PlayerId != player?.Id) return;

            results.Player = player;
            keySelector.SetSelectedKeys(results);
            _bus.OnPlayerSelectedKeys(results);

            if (keySelector.Locked)
            {
                _stateManager.LockAll();
                await Clients.All.SendAsync("LockAll");
                _bus.OnAllPlayersSelectedKeys(keySelector.KeyResultsForPlayer);
            }
        }
    }
}