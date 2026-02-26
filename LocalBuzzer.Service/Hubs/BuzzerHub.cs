using LocalBuzzer.Service.Base;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Hubs
{
    public sealed class BuzzerHub : Hub
    {
        private static int _counter;
        private static readonly ConcurrentDictionary<string, string> NamesByConn = new();

        private readonly BuzzerState _state;
        private readonly BuzzerEventBus _bus;

        public BuzzerHub(BuzzerState state, BuzzerEventBus bus)
        {
            _state = state;
            _bus = bus;
        }

        public override async Task OnConnectedAsync()
        {
            // optional: ?name=Max
            var http = Context.GetHttpContext();
            var requestedName = http?.Request.Query["name"].ToString();
            var name = string.IsNullOrWhiteSpace(requestedName)
                ? $"Spieler {Interlocked.Increment(ref _counter)}"
                : requestedName.Trim();

            NamesByConn[Context.ConnectionId] = name;
            _bus.OnAssigned(name);

            await Clients.Caller.SendAsync("Assigned", name, _state.Round, _state.Locked, _state.Winner);
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            NamesByConn.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task Buzz()
        {
            NamesByConn.TryGetValue(Context.ConnectionId, out var name);
            name ??= "Unbekannt";

            if (_state.TryBuzz(name))
            {
                _bus.OnWinner(name, _state.Round);
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