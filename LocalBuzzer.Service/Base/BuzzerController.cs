using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    public class BuzzerController : IDisposable
    {
        public BuzzerEventBus EventBus { get; internal set; }

        public LayoutStateManager StateManager { get; internal set; }

        public IHubContext<BuzzerHub> HubContext { get; internal set; }

        public BuzzerController(BuzzerEventBus eventBus, IHubContext<BuzzerHub> hubContext, LayoutStateManager stateManager)
        {
            EventBus = eventBus;
            HubContext = hubContext;
            StateManager = stateManager;
        }

        public async Task ResetRoundAsync(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None, CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.ResetLayouts(round, layout);
            EventBus.OnReset(round);
            await HubContext.Clients.All.SendAsync("Reset", round, layout, ct);
        }

        public async Task LockAllAsync()
        {
            StateManager.LockAll();
            await HubContext.Clients.All.SendAsync("LockAll");
        }

        private void CheckServerRunning()
        {
            if (HubContext is null || EventBus is null || StateManager is null)
                throw new InvalidOperationException("Server is not running.");
        }

        public void Dispose()
        {
            HubContext = null!;
            EventBus = null!;
            StateManager = null!;
        }
    }
}