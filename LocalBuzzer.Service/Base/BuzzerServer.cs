using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Hubs;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Quizzer.DataModels.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LocalBuzzer.Service
{
    public sealed class BuzzerServer : IAsyncDisposable
    {
        private WebApplication? _app;
        private Task? _runTask;

        public event Action<string>? ClientAssigned;

        public event Action<Player?, int>? WinnerDeclared;

        public event Action<int>? RoundReset;

        private IHubContext<BuzzerHub>? _hub;
        private BuzzerState? _state;

        public Func<Game?>? GetGame { get; set; }

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
            builder.Services.AddSingleton<GameAccessor>();
            builder.Services.AddSignalR();

            var app = builder.Build();

            _hub = app.Services.GetRequiredService<IHubContext<BuzzerHub>>();
            _state = app.Services.GetRequiredService<BuzzerState>();
            var gameAccessor = app.Services.GetRequiredService<GameAccessor>();
            gameAccessor.GetGame = GetGame;

            var fp = app.Services.GetRequiredService<PhysicalFileProvider>();
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fp });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fp });

            app.MapHub<BuzzerHub>("/hub");

            var bus = app.Services.GetRequiredService<BuzzerEventBus>();

            bus.ClientAssigned += p => ClientAssigned?.Invoke(p);
            bus.WinnerDeclared += (p, r) => WinnerDeclared?.Invoke(p, r);
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

        public IReadOnlyList<string> GetListeningIpPorts(bool includeLoopback = false, bool includeIPv6 = false)
        {
            if (_app is null) return Array.Empty<string>();

            // Get the real server-bound addresses (after StartAsync)
            var server = _app.Services.GetRequiredService<IServer>();
            var feature = server.Features.Get<IServerAddressesFeature>();
            var urls = (feature?.Addresses?.Count > 0 ? feature.Addresses : _app.Urls).ToList();

            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var url in urls)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    continue;

                var host = uri.Host;
                var port = uri.Port;

                bool isWildcard =
                    host == "0.0.0.0" || host == "*" || host == "+" ||
                    host == "::" || host == "[::]" ||
                    host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

                if (!isWildcard)
                {
                    results.Add($"{host}:{port}");
                    continue;
                }

                // Expand wildcard/localhost to actual interface IPs
                foreach (var ip in GetLocalIPs(includeLoopback, includeIPv6))
                    results.Add($"{ip}:{port}");
            }

            return results.OrderBy(s => s).ToArray();
        }

        private static IEnumerable<string> GetLocalIPs(bool includeLoopback, bool includeIPv6)
        {
            // IPv4 only (phones will use this anyway)
            var candidates =
                from ni in NetworkInterface.GetAllNetworkInterfaces()
                where ni.OperationalStatus == OperationalStatus.Up
                where ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                where ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                where !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                where !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                where !ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase)
                where !ni.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase)
                let props = ni.GetIPProperties()
                let hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address != null &&
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.Equals(IPAddress.Any) &&
                    !g.Address.Equals(IPAddress.None))
                where hasGateway // IMPORTANT: pick the "real" LAN interface
                from ua in props.UnicastAddresses
                let ip = ua.Address
                where ip.AddressFamily == AddressFamily.InterNetwork
                where includeLoopback || !IPAddress.IsLoopback(ip)
                select ip.ToString();

            // Prefer 192.168.* then 10.* then anything else
            return candidates
                .Distinct()
                .OrderByDescending(ip => ip.StartsWith("192.168.", StringComparison.Ordinal))
                .ThenByDescending(ip => ip.StartsWith("10.", StringComparison.Ordinal))
                .ThenBy(ip => ip)
                .ToArray();
        }

        public string? GetBestListeningIpPort()
        {
            return GetListeningIpPorts()
                .OrderByDescending(s => s.StartsWith("192.168."))
                .ThenByDescending(s => s.StartsWith("10."))
                .FirstOrDefault();
        }
    }
}