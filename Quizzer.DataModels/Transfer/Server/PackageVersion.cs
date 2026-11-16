using System.Globalization;
using System.Text.RegularExpressions;

namespace Quizzer.DataModels.Transfer.Server
{
    /// <summary>
    /// Die Version eines WPF-Pakets (M36 A7): <c>yyyyMMdd-HHmm-&lt;Commit&gt;</c>, Zeit in UTC, gesetzt von
    /// <c>deploy/wpf-paket.ps1</c>. Ein Entwicklungsbau traegt keine und bietet nie eine Aktualisierung an.
    /// Der Text wird zu Datei- und Ordnernamen - deshalb nur dieses enge Muster.
    /// </summary>
    public sealed partial record PackageVersion(DateTime Stamp, string Commit)
    {
        private const string StampFormat = "yyyyMMdd-HHmm";

        [GeneratedRegex(@"^([0-9]{8}-[0-9]{4})-([0-9a-f]{7,40})\z")]
        private static partial Regex Pattern();

        /// <summary>Die kanonische Schreibweise - nur sie wird Datei- oder Ordnername.</summary>
        public string Text => Stamp.ToString(StampFormat, CultureInfo.InvariantCulture) + "-" + Commit;

        /// <summary>
        /// <c>null</c> fuer einen Entwicklungsbau oder Unlesbares. Ein vom SDK angehaengtes
        /// <c>+Quellstand</c> zaehlt nicht mit.
        /// </summary>
        public static PackageVersion? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var plus = text.IndexOf('+');
            var match = Pattern().Match(plus >= 0 ? text[..plus] : text);

            if (!match.Success || !DateTime.TryParseExact(match.Groups[1].Value, StampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var stamp))
                return null;

            return new PackageVersion(stamp, match.Groups[2].Value);
        }

        /// <summary>Wie <see cref="TryParse"/>, aber nur die kanonische Schreibweise ohne Zusatz.</summary>
        public static PackageVersion? TryParseExact(string? text)
            => TryParse(text) is { } version && version.Text == text ? version : null;

        /// <summary>Neuer heisst spaeter gebaut; derselbe Zeitpunkt ist nicht neuer.</summary>
        public bool IsNewerThan(PackageVersion other) => Stamp > other.Stamp;

        /// <summary>Ob <paramref name="candidate"/> das laufende Programm ersetzen soll - nie bei einem Entwicklungsbau.</summary>
        public static bool IsUpdate(string? candidate, string? running)
            => TryParse(running) is { } current && TryParseExact(candidate) is { } next && next.IsNewerThan(current);
    }
}
