using System.Text.RegularExpressions;

namespace Quizzer.DataModels.Transfer.Server
{
    /// <summary>
    /// Welche Medienpfade zwischen Server und WPF wandern duerfen, relativ zum Datenordner
    /// (<c>Settings.FilePathQuizzer</c>) und mit <c>/</c> getrennt:
    /// <list type="bullet">
    /// <item><c>Resources/&lt;Datei&gt;</c> - Medien der Fragen</item>
    /// <item><c>Themes/&lt;Ordner&gt;/&lt;Datei&gt;</c> - Texturen eines Designs</item>
    /// <item><c>&lt;Datei&gt;</c> - Spielerbilder und die Grundtexturen</item>
    /// </list>
    /// Alles andere wird abgewiesen, bevor es ein Dateisystem sieht (standards-sicherheit A01).
    /// </summary>
    public static partial class MediaPaths
    {
        public const string ResourcesFolder = "Resources";

        public const string ThemesFolder = "Themes";

        /// <summary>Dieselben Endungen, die <c>FileHelper.DetectResourceType</c> annimmt.</summary>
        public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm", ".m4v",
            ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac",
            ".pdf", ".doc", ".docx", ".txt",
        };

        [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} _.\-()+]{0,199}$")]
        private static partial Regex SegmentPattern();

        /// <summary>Prueft einen relativen Pfad und liefert ihn in der kanonischen Form.</summary>
        public static bool TryNormalize(string? path, out string normalized)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(path) || path.Length > 400)
                return false;

            var segments = path.Replace('\\', '/').Split('/');

            if (segments.Any(s => !IsValidSegment(s)))
                return false;

            var valid = segments.Length switch
            {
                1 => true,
                2 => string.Equals(segments[0], ResourcesFolder, StringComparison.OrdinalIgnoreCase),
                3 => string.Equals(segments[0], ThemesFolder, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };

            if (!valid || !AllowedExtensions.Contains(Path.GetExtension(segments[^1])))
                return false;

            if (segments.Length >= 2)
                segments[0] = segments.Length == 2 ? ResourcesFolder : ThemesFolder;

            normalized = string.Join('/', segments);
            return true;
        }

        private static bool IsValidSegment(string segment)
            => segment != "." && segment != ".." && !segment.Contains("..") && SegmentPattern().IsMatch(segment);

        public static string ForResource(string fileName) => $"{ResourcesFolder}/{fileName}";

        public static string ForTheme(string folder, string fileName) => $"{ThemesFolder}/{folder}/{fileName}";

        /// <summary>
        /// Der volle Pfad unter <paramref name="root"/> - oder <c>null</c>, wenn er trotz aller
        /// Pruefungen ausserhalb laege (zweite Linie hinter <see cref="TryNormalize"/>).
        /// </summary>
        public static string? ToFullPath(string root, string normalized)
        {
            var rootFull = Path.GetFullPath(root);
            var full = Path.GetFullPath(Path.Combine(rootFull, normalized.Replace('/', Path.DirectorySeparatorChar)));

            var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.Ordinal) ? full : null;
        }
    }
}
