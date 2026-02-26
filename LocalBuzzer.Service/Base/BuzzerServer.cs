using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace LocalBuzzer.Service
{
    public sealed class BuzzerServer : IAsyncDisposable
    {
        private WebApplication? _app;
        private Task? _runTask;

        public event Action<string>? ClientAssigned;

        public event Action<string, int>? WinnerDeclared;

        public event Action<int>? RoundReset;

        private IHubContext<BuzzerHub>? _hub;
        private BuzzerState? _state;

        public bool IsRunning => _app is not null;

        public async Task StartAsync(BuzzerServerOptions? options = null, CancellationToken ct = default)
        {
            if (_app is not null) return;
            options ??= new BuzzerServerOptions();

            if (!Directory.Exists(options.WebRootPath))
                throw new DirectoryNotFoundException($"wwwroot nicht gefunden: {options.WebRootPath}");

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");

            builder.Services.AddSingleton(new PhysicalFileProvider(options.WebRootPath));
            builder.Services.AddSingleton<BuzzerState>();
            builder.Services.AddSingleton<BuzzerEventBus>();
            builder.Services.AddSignalR();

            var app = builder.Build();

            _hub = app.Services.GetRequiredService<IHubContext<BuzzerHub>>();
            _state = app.Services.GetRequiredService<BuzzerState>();

            var fp = app.Services.GetRequiredService<PhysicalFileProvider>();
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fp });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fp });

            app.MapHub<BuzzerHub>("/hub");

            var bus = app.Services.GetRequiredService<BuzzerEventBus>();

            bus.ClientAssigned += n => ClientAssigned?.Invoke(n);
            bus.WinnerDeclared += (n, r) => WinnerDeclared?.Invoke(n, r);
            bus.RoundReset += r => RoundReset?.Invoke(r);

            _app = app;

            await _app.StartAsync(ct);
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (_app is null) return;

            await _app.StopAsync(ct);
            await _app.DisposeAsync();

            _app = null;
        }

        public async Task ResetRoundAsync(CancellationToken ct = default)
        {
            if (_hub is null || _state is null)
                throw new InvalidOperationException("Server is not running.");

            // reset state
            var newRound = _state.Reset(); // make Reset return int (round) or read _state.Round after Reset()

            // notify all clients
            await _hub.Clients.All.SendAsync("Reset", newRound, ct);

            // notify WPF subscribers
            RoundReset?.Invoke(newRound);
        }

        public ValueTask DisposeAsync() => new(StopAsync());
    }
}