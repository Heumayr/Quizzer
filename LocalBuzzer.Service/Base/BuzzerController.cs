using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public class BuzzerController : IDisposable
    {
        public BuzzerEventBus EventBus { get; internal set; }

        public BuzzerState State { get; internal set; }

        public IHubContext<BuzzerHub> HubContext { get; internal set; }

        public BuzzerController(BuzzerEventBus eventBus, IHubContext<BuzzerHub> hubContext, BuzzerState state)
        {
            EventBus = eventBus;
            HubContext = hubContext;
            State = state;
        }

        public async Task ResetRoundAsync(int round, CancellationToken ct = default)
        {
            CheckServerRunning();

            State.Reset(round);
            EventBus.OnReset(round);
            await HubContext.Clients.All.SendAsync("Reset", round, ct);
        }

        private void CheckServerRunning()
        {
            if (HubContext is null || EventBus is null || State is null)
                throw new InvalidOperationException("Server is not running.");
        }

        public void Dispose()
        {
            HubContext = null!;
            EventBus = null!;
            State = null!;
        }
    }
}