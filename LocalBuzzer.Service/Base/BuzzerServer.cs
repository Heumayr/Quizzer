using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Hubs;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LocalBuzzer.Service
{
    public sealed class BuzzerServer : IAsyncDisposable
    {
        private WebApplication? _app;

        public BuzzerController? BuzzerController { get; private set; }

        public Func<Game?>? GetGame { get; set; }

        private readonly SemaphoreSlim _gate = new(1, 1);

        public ServerState ServerState { get; private set; } = ServerState.None;

        public async Task StartAsync(BuzzerServerOptions? options = null, CancellationToken ct = default, bool rebuild = false)
        {
            await _gate.WaitAsync(ct);
            try
            {
                // If already running and no rebuild requested => no-op
                if (_app != null && !rebuild && ServerState == ServerState.Running)
                    return;

                // If something exists (running or error), dispose it before rebuild/start
                if (_app != null)
                    await StopAsync(ct);

                ServerState = ServerState.Starting;

                options ??= new BuzzerServerOptions();
                if (!Directory.Exists(options.WebRootPath))
                    throw new DirectoryNotFoundException($"wwwroot nicht gefunden: {options.WebRootPath}");

                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseKestrel();
                builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");

                builder.Services.AddSingleton(new PhysicalFileProvider(options.WebRootPath));
                builder.Services.AddSingleton<LayoutStateManager>();
                builder.Services.AddSingleton<BuzzerEventBus>();
                builder.Services.AddSingleton<GameAccessor>();
                builder.Services.AddSingleton<PlayerConnectionRegistry>();
                builder.Services.AddSignalR();
                builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);

                var app = builder.Build(); // local until started successfully

                try
                {
                    var gameAccessor = app.Services.GetRequiredService<GameAccessor>();
                    gameAccessor.GetGame = GetGame;

                    var fp = app.Services.GetRequiredService<PhysicalFileProvider>();
                    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fp });
                    app.UseStaticFiles(new StaticFileOptions { FileProvider = fp });

                    app.MapHub<BuzzerHub>("/hub");

                    var eventBus = app.Services.GetRequiredService<BuzzerEventBus>();
                    var buzzerHubContext = app.Services.GetRequiredService<IHubContext<BuzzerHub>>();
                    var stateManager = app.Services.GetRequiredService<LayoutStateManager>();

                    BuzzerController?.Dispose();
                    BuzzerController = new BuzzerController(eventBus, buzzerHubContext, stateManager);

                    await app.StartAsync(ct);

                    _app = app;                 // assign only after successful start
                    ServerState = ServerState.Running;
                }
                catch
                {
                    await app.DisposeAsync();    // prevent leaks
                    BuzzerController?.Dispose();
                    ServerState = ServerState.Error;
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (_app == null)
                {
                    ServerState = ServerState.None;
                    BuzzerController?.Dispose();
                    return;
                }

                ServerState = ServerState.Stopping;

                try
                {
                    await _app.StopAsync(ct);
                    await _app.DisposeAsync();
                    ServerState = ServerState.None;
                }
                catch
                {
                    ServerState = ServerState.Error;
                    throw;
                }
                finally
                {
                    _app = null;
                    BuzzerController?.Dispose();
                }
            }
            finally
            {
                _gate.Release();
            }
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

        /// <summary>
        /// Liest die Adapter dieses Rechners und laesst
        /// <see cref="NetworkAddressPicker"/> waehlen.
        /// <para>
        /// <b>Die Regel steht seit 2026-09-09 nicht mehr hier</b>, sondern im Picker - hier
        /// war sie eine LINQ-Abfrage ueber den Zustand dieses Rechners und damit von keiner
        /// Zusicherung erreichbar. Diese Methode macht jetzt nur noch das, was sich nicht
        /// zusichern laesst: das Ablesen.
        /// </para>
        /// </summary>
        private static IEnumerable<string> GetLocalIPs(bool includeLoopback, bool includeIPv6)
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces().Select(ni =>
            {
                var props = ni.GetIPProperties();

                return new NetworkAdapterInfo(
                    ni.Description,
                    ni.OperationalStatus == OperationalStatus.Up,
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback,
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel,
                    props.GatewayAddresses.Any(g =>
                        g.Address != null &&
                        g.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !g.Address.Equals(IPAddress.Any) &&
                        !g.Address.Equals(IPAddress.None)),
                    props.UnicastAddresses
                        .Select(ua => ua.Address)
                        .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                        .Select(ip => ip.ToString())
                        .ToArray());
            });

            return NetworkAddressPicker.Pick(adapter, includeLoopback);
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