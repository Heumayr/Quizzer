using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Server
{
    /// <summary>
    /// Aktualisierung aus dem Anmeldefenster (M36 A7): liegt auf dem Server ein neueres Paket - oder
    /// weist er dieses Programm als zu alt ab -, steht hier „herunterladen und neu starten".
    /// </summary>
    public sealed partial class ServerLoginViewModel
    {
        private const string NoPackageLeft = "Auf dem Server liegt kein Quizzer-Paket mehr.";

        private ClientPackageInfo? offeredUpdate;

        public string UpdateText => offeredUpdate == null ? string.Empty
            : $"Neue Version verfügbar ({Describe(offeredUpdate.Version)}) – herunterladen und neu starten";

        public Visibility UpdateVisibility => offeredUpdate == null ? Visibility.Collapsed : Visibility.Visible;

        internal static string Describe(string version)
            => PackageVersion.TryParseExact(version) is { } v
                ? v.Stamp.ToLocalTime().ToString("dd.MM.yyyy, HH:mm", CultureInfo.GetCultureInfo("de-DE"))
                : version;

        /// <summary>Fragt nach einem neueren Paket. Scheitert still - die Anmeldung haengt nie daran.</summary>
        private async Task CheckForUpdateAsync(QuizzerApiClient? api)
        {
            ClientPackageInfo? latest = null;
            try
            {
                if (api != null)
                    latest = await api.GetClientPackageAsync();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Paketabfrage gescheitert: " + ex.Message);
            }

            offeredUpdate = ClientUpdate.IsOffered(latest, ClientUpdate.RunningVersion) ? latest : null;
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(UpdateVisibility));
        }

        private AsyncRelayCommand? updateCommand;

        public ICommand UpdateCommand => updateCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (offeredUpdate is not { } package || ServerSession.Api is not { } api)
                return;

            Busy = "Neue Version wird geladen …";
            var progress = new DownloadProgress(package.Size, percent => Busy = $"Neue Version wird geladen … {percent} %");

            try
            {
                var exe = await ClientUpdate.InstallAsync(package, async (target, ct) =>
                {
                    if (!await api.DownloadClientPackageAsync(target, progress, ct))
                        throw new InvalidDataException(NoPackageLeft);
                }, ClientUpdate.Root);

                Busy = "Neue Version wird gestartet …";
                SignedIn = false;
                ClientUpdate.StartAndExit(exe);
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException or InvalidDataException
                                           or IOException or UnauthorizedAccessException or Win32Exception)
            {
                Error = "Die Aktualisierung ist gescheitert: " + ex.Message;
                Busy = string.Empty;
            }
        });

        /// <summary>
        /// Meldet nur bei neuem Prozentwert, und zwar sofort: <see cref="Progress{T}"/> stellte die Meldung
        /// hinten an, und eine spaete koennte ein schon geraeumtes „Busy" wieder setzen.
        /// </summary>
        private sealed class DownloadProgress(long size, Action<long> report) : IProgress<long>
        {
            private long last = -1;

            public void Report(long value)
            {
                var percent = Math.Clamp(value * 100 / Math.Max(1, size), 0, 100);
                if (percent == last)
                    return;

                last = percent;
                report(percent);
            }
        }
    }
}
