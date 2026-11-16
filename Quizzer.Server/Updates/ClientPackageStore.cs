using Microsoft.Extensions.Options;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server.Infrastructure;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Quizzer.Server.Updates
{
    /// <summary>
    /// Das WPF-Paket zum Aktualisieren (M36 A7): <c>client/Quizzer-&lt;Version&gt;.zip</c> unter der
    /// Medienwurzel. Kein Teil des Pfads kommt von aussen; liegen mehrere Pakete da, gilt das neueste.
    /// Die Pruefsumme wird neu gerechnet, sobald sich Datei, Groesse oder Zeitstempel aendern.
    /// </summary>
    public sealed partial class ClientPackageStore(IOptions<ServerOptions> options, ILogger<ClientPackageStore> log)
    {
        public const string Folder = "client";

        [GeneratedRegex(@"^Quizzer-(.+)\.zip\z")]
        private static partial Regex FileNamePattern();

        private readonly SemaphoreSlim gate = new(1, 1);
        private Measured? last;

        public sealed record Package(string FullPath, PackageVersion Version);

        private sealed record Measured(string FullPath, long Size, DateTime WrittenUtc, ClientPackageInfo Info);

        public static string FileName(PackageVersion version) => $"Quizzer-{version.Text}.zip";

        /// <summary>Das neueste Paket, oder <c>null</c>. Nur Namen im Muster mit kanonischer Version zaehlen.</summary>
        public Package? Find()
        {
            var folder = Path.Combine(Path.GetFullPath(options.Value.MediaRoot), Folder);
            if (!Directory.Exists(folder))
                return null;

            Package? newest = null;

            foreach (var full in Directory.EnumerateFiles(folder, "Quizzer-*.zip", SearchOption.TopDirectoryOnly))
            {
                var match = FileNamePattern().Match(Path.GetFileName(full));
                if (!match.Success || PackageVersion.TryParseExact(match.Groups[1].Value) is not { } version)
                    continue;

                if (newest == null || version.IsNewerThan(newest.Version))
                    newest = new Package(full, version);
            }

            return newest;
        }

        /// <summary>Version, Groesse und SHA-256 des neuesten Pakets, oder <c>null</c>.</summary>
        public async Task<ClientPackageInfo?> InfoAsync(CancellationToken ct)
        {
            if (Find() is not { } package)
                return null;

            var file = new FileInfo(package.FullPath);

            await gate.WaitAsync(ct);
            try
            {
                if (last is { } m && m.FullPath == file.FullName && m.Size == file.Length && m.WrittenUtc == file.LastWriteTimeUtc)
                    return m.Info;

                string hash;
                await using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));

                var info = new ClientPackageInfo(package.Version.Text, file.Length, hash);
                last = new Measured(file.FullName, file.Length, file.LastWriteTimeUtc, info);
                log.LogInformation("WPF-Paket {Version} bereit ({Bytes} Byte)", info.Version, info.Size);
                return info;
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
