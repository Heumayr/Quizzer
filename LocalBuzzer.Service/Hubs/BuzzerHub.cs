using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Enumerations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Hubs
{
    public sealed class BuzzerHub : Hub
    {
        private static int _counter;
        private static readonly ConcurrentDictionary<string, Player> PlayerByConn = new();

        private readonly BuzzerState _state;
        private readonly BuzzerEventBus _bus;

        private readonly GameAccessor _gameAccessor;

        public BuzzerHub(BuzzerState state, BuzzerEventBus bus, GameAccessor gameAccessor)
        {
            _state = state;
            _bus = bus;
            _gameAccessor = gameAccessor;
        }

        public override async Task OnConnectedAsync()
        {
            var game = _gameAccessor.GetGame?.Invoke();

            if (game == null)
                return;

            var http = Context.GetHttpContext();
            var qs = Context.GetHttpContext()?.Request.Query["playerid"].ToString();

            if (!Guid.TryParse(qs, out var guid))
                throw new HubException("Invalid or missing player id");

            var player = game.Players.FirstOrDefault(p => p.Id == guid);

            if (player == null)
                throw new Exception("Player not found");

            PlayerByConn[Context.ConnectionId] = player;
            var name = player.CalculatedDisplayName;
            _bus.OnAssigned(name);

            Interlocked.Increment(ref _counter);

            await Clients.Caller.SendAsync("Assigned", name, _state.Round, _state.Locked, _state.Winner);
            player.ConnectionState = PlayerConnection.Connected;
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
            PlayerByConn.TryGetValue(Context.ConnectionId, out var player);
            var name = player?.CalculatedDisplayName ?? "Unknown";

            if (_state.TryBuzz(player))
            {
                _bus.OnWinner(player, _state.Round);
                await Clients.All.SendAsync("Winner", name, _state.Round);
            }
        }

        public async Task ResetRound()
        {
            var round = _state.Reset();
            _bus.OnReset(round);
            await Clients.All.SendAsync("Reset", round);
        }
    }
}