using Quizzer.DataModels.Transfer.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;

namespace Quizzer.Server
{
    /// <summary>
    /// Aktualisierung des WPF ueber den Server (M36 A7). Neue Versionen landen unter
    /// <c>%LOCALAPPDATA%\Quizzer\app\&lt;Version&gt;\</c>; die erste Installation startet beim Start die
    /// neueste davon, damit die Verknuepfung auf sie weiter stimmt. Ein Entwicklungsbau tut nichts davon.
    /// </summary>
    public static class ClientUpdate
    {
        public const string ExeName = "Quizzer.exe";

        /// <summary>Wird als Letztes geschrieben - ohne ihn gilt ein Ordner nicht als vollstaendig.</summary>
        internal const string CompleteMarker = "quizzer-paket.ok";

        internal const string ChecksumMismatch = "Die heruntergeladene Datei ist unvollständig oder verändert (Prüfsumme stimmt nicht). Bitte noch einmal versuchen.";

        internal const string PathEscape = "Das Paket enthält einen Pfad außerhalb seines Ordners und wird nicht entpackt.";

        internal const string NoProgram = "Das Paket enthält kein Quizzer.exe.";

        internal const string InvalidVersion = "Der Server nennt keine gültige Paketversion.";

        /// <summary>Wohin neue Versionen kommen (Quizzer\app unter LOCALAPPDATA); Tests setzen einen eigenen Ordner.</summary>
        internal static string Root { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Quizzer", "app");

        /// <summary>Die Paketversion dieses Programms (leer oder unlesbar: Entwicklungsbau); Tests setzen eine andere.</summary>
        internal static string? RunningVersion { get; set; } =
            typeof(ClientUpdate).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        /// <summary>Startet die neue Quizzer.exe und beendet dieses Programm; Tests setzen eine Attrappe.</summary>
        internal static Action<string> StartAndExit { get; set; } = exe =>
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
            Application.Current.Shutdown();
        };

        /// <summary>Ob das Anmeldefenster das Paket anbietet: nur ein neueres, nie einem Entwicklungsbau.</summary>
        public static bool IsOffered(ClientPackageInfo? latest, string? running)
            => latest != null && PackageVersion.IsUpdate(latest.Version, running);

        /// <summary>Die Quizzer.exe der neuesten vollstaendigen Version unter <paramref name="root"/>, wenn sie neuer ist als die laufende.</summary>
        public static string? FindNewerInstalled(string root, string? running)
        {
            if (PackageVersion.TryParse(running) is not { } current || !Directory.Exists(root))
                return null;

            (string Folder, PackageVersion Version)? newest = null;

            foreach (var folder in Directory.EnumerateDirectories(root))
            {
                if (PackageVersion.TryParseExact(Path.GetFileName(folder)) is not { } version
                    || !version.IsNewerThan(current) || !IsComplete(folder))
                    continue;

                if (newest == null || version.IsNewerThan(newest.Value.Version))
                    newest = (folder, version);
            }

            return newest == null ? null : Path.Combine(newest.Value.Folder, ExeName);
        }

        internal static bool IsComplete(string folder)
            => File.Exists(Path.Combine(folder, ExeName)) && File.Exists(Path.Combine(folder, CompleteMarker));

        /// <summary>
        /// Beim Start: liegt eine neuere Version bereit, wird sie gestartet (<c>true</c>, dieses Programm endet).
        /// Scheitert das, laeuft dieses weiter - ein kaputter Ordner darf den Start nicht verhindern.
        /// </summary>
        internal static bool TryStartNewerInstalled(IReadOnlyList<string> args)
        {
            try
            {
                if (FindNewerInstalled(Root, RunningVersion) is not { } exe)
                    return false;

                var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
                foreach (var arg in args)
                    start.ArgumentList.Add(arg);

                Process.Start(start);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
            {
                Trace.TraceWarning("Neuere Quizzer-Version nicht gestartet: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Laedt das Paket, prueft die Pruefsumme <b>vor</b> dem Entpacken und entpackt in einen Nebenordner,
        /// der erst am Ende seinen Versionsnamen bekommt. Liefert die neue Quizzer.exe; wirft mit zeigbarer Meldung.
        /// </summary>
        public static async Task<string> InstallAsync(ClientPackageInfo info, Func<Stream, CancellationToken, Task> download,
            string root, CancellationToken ct = default)
        {
            var version = PackageVersion.TryParseExact(info.Version) ?? throw new InvalidDataException(InvalidVersion);
            var target = Path.Combine(root, version.Text);
            if (IsComplete(target))
                return Path.Combine(target, ExeName);

            Directory.CreateDirectory(root);
            var file = target + ".download";
            var partial = target + ".partial";

            try
            {
                await using (var output = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                    await download(output, ct);

                await VerifyAsync(file, info, ct);

                if (Directory.Exists(partial))
                    Directory.Delete(partial, recursive: true);

                Extract(file, partial);

                if (!File.Exists(Path.Combine(partial, ExeName)))
                    throw new InvalidDataException(NoProgram);

                await File.WriteAllTextAsync(Path.Combine(partial, CompleteMarker), info.Sha256, ct);

                if (Directory.Exists(target))
                    Directory.Delete(target, recursive: true);

                Directory.Move(partial, target);
                return Path.Combine(target, ExeName);
            }
            finally
            {
                TryDelete(file, partial);
            }
        }

        internal static async Task VerifyAsync(string file, ClientPackageInfo info, CancellationToken ct)
        {
            await using var stream = File.OpenRead(file);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));

            if (stream.Length != info.Size || !string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(ChecksumMismatch);
        }

        /// <summary>Entpackt nur innerhalb von <paramref name="folder"/>; ein Eintrag wie <c>..\x</c> bricht ab.</summary>
        internal static void Extract(string zipFile, string folder)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);

            using var archive = ZipFile.OpenRead(zipFile);

            foreach (var entry in archive.Entries)
            {
                var full = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(PathEscape);

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(full);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                entry.ExtractToFile(full, overwrite: false);
            }
        }

        private static void TryDelete(string file, string folder)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);

                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Reste stoeren nicht: der naechste Versuch raeumt sie vor dem Entpacken weg.
                Trace.TraceWarning("Reste der Aktualisierung nicht entfernt: " + ex.Message);
            }
        }
    }
}
