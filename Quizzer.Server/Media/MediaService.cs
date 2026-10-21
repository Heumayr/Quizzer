using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Context;
using Quizzer.Server.Auth;
using Quizzer.Server.Infrastructure;
using Quizzer.Server.Sync;
using System.Security.Cryptography;

namespace Quizzer.Server.Media
{
    /// <summary>
    /// Die Medienablage am Server (ein Volume im Container). Abgerufen werden darf nur, was ein
    /// Konto ueber eine sichtbare Zeile erreicht: die Medien seiner sichtbaren Fragen, die Bilder
    /// der Mitspieler, die Design-Ordner und die Grundtexturen (standards-sicherheit A01:
    /// Objektbezug, nicht nur Funktion).
    /// </summary>
    public sealed class MediaService(IOptions<ServerOptions> options, ILogger<MediaService> log)
    {
        private readonly ServerOptions settings = options.Value;

        public string Root => settings.MediaRoot;

        public long MaxBytes => settings.MaxMediaBytes;

        /// <summary>Die Grundtexturen, die <c>Settings</c> unter festen Namen sucht.</summary>
        private static readonly HashSet<string> SkinFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "AudioPlaceholderFile.png", "Background.png", "PlaceholderPlayer.png", "PlayerCardBackground.png",
            "PlayerCardBackgroundWinner.png", "CellBackground.png", "CellBackgroundHover.png",
            "CellBackgroundIsDone.png", "HeaderColumnBackground.png", "HeaderRowBackground.png",
            "GridBackground.png", "GridBackgroundResult.png", "HorizontalBackground.png",
        };

        /// <summary>Die Pfade, die das Konto abrufen darf (Themes als Ordnerpraefix).</summary>
        public async Task<(HashSet<string> Files, HashSet<string> ThemeFolders)> AllowedAsync(CurrentAccount account, CancellationToken ct)
        {
            await using var context = new DataContext();
            var questions = Visibility.QuestionIds(context, account);

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in await context.QuestionStepResources
                         .Where(s => questions.Contains(s.QuestionBaseId) && s.ResourceFileName != "")
                         .Select(s => s.ResourceFileName).ToListAsync(ct))
            {
                files.Add(MediaPaths.ForResource(name));
            }

            foreach (var name in await context.RevealQuestions
                         .Where(q => questions.Contains(q.Id) && q.ImageFileName != "")
                         .Select(q => q.ImageFileName).ToListAsync(ct))
            {
                files.Add(MediaPaths.ForResource(name));
            }

            foreach (var name in await context.Players.Where(p => p.UserPictureFileName != "")
                         .Select(p => p.UserPictureFileName).ToListAsync(ct))
            {
                files.Add(name);
            }

            files.UnionWith(SkinFiles);

            var themes = (await context.GameThemes.Where(t => t.FolderName != "").Select(t => t.FolderName).ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return (files, themes);
        }

        public static bool IsAllowed(string normalized, HashSet<string> files, HashSet<string> themeFolders)
        {
            if (files.Contains(normalized))
                return true;

            var segments = normalized.Split('/');
            return segments.Length == 3 && themeFolders.Contains(segments[1]);
        }

        public async Task<List<MediaFileInfo>> ManifestAsync(CurrentAccount account, CancellationToken ct)
        {
            var (files, themes) = await AllowedAsync(account, ct);
            var result = new List<MediaFileInfo>();

            if (!Directory.Exists(Root))
                return result;

            foreach (var full in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, full).Replace(Path.DirectorySeparatorChar, '/');

                if (!MediaPaths.TryNormalize(relative, out var normalized) || !IsAllowed(normalized, files, themes))
                    continue;

                result.Add(new MediaFileInfo(normalized, new FileInfo(full).Length, await HashAsync(full, ct)));
            }

            return result;
        }

        public async Task<string?> OpenPathAsync(CurrentAccount account, string? path, CancellationToken ct)
        {
            if (!MediaPaths.TryNormalize(path, out var normalized))
                return null;

            var (files, themes) = await AllowedAsync(account, ct);

            if (!IsAllowed(normalized, files, themes))
                return null;

            var full = MediaPaths.ToFullPath(Root, normalized);
            return full != null && File.Exists(full) ? full : null;
        }

        public enum UploadResult { Created, Unchanged, Invalid, TooLarge, Exists }

        /// <summary>
        /// Nimmt eine Datei an. <b>Ueberschrieben wird nie</b>: liegt unter dem Namen schon eine
        /// andere Datei, gibt es <see cref="UploadResult.Exists"/>. Die Namen tragen eine Guid
        /// (<c>FileHelper</c>), ein Zusammenstoss ist also ein Versehen oder ein Angriff.
        /// </summary>
        public async Task<UploadResult> UploadAsync(string? path, Stream body, CancellationToken ct)
        {
            if (!MediaPaths.TryNormalize(path, out var normalized))
                return UploadResult.Invalid;

            var full = MediaPaths.ToFullPath(Root, normalized);
            if (full == null)
                return UploadResult.Invalid;

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var temp = full + "." + Guid.NewGuid().ToString("N") + ".upload";

            try
            {
                long total = 0;
                await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await body.ReadAsync(buffer, ct)) > 0)
                    {
                        total += read;
                        if (total > settings.MaxMediaBytes)
                            return UploadResult.TooLarge;

                        await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                }

                if (File.Exists(full))
                {
                    return await HashAsync(full, ct) == await HashAsync(temp, ct)
                        ? UploadResult.Unchanged
                        : UploadResult.Exists;
                }

                File.Move(temp, full);
                log.LogInformation("Medium angenommen: {Path} ({Bytes} Byte)", normalized, total);
                return UploadResult.Created;
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        private static async Task<string> HashAsync(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        }
    }
}
