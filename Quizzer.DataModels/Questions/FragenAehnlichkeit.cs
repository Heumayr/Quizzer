using System.Globalization;
using System.Text;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Findet schon vorhandene Fragen mit fast demselben Fragetext (M32, 2026-10-23). Mit
    /// KI-Entwürfen entsteht leicht dieselbe Frage zweimal - etwa „Hauptstadt von Peru?" in zwei
    /// Runden zum selben Thema.
    /// <para>
    /// <b>Gemessen wird über Wörter, nicht über Zeichen:</b> der Anteil gemeinsamer Wörter an
    /// allen (Jaccard) oder an der kürzeren Frage, ohne Groß/klein, Satzzeichen, Akzente und Füllwörter. „Wie hoch ist der
    /// Großglockner?" und „Wie hoch ist der Großglockner in Metern?" gelten als ähnlich, „Wie hoch
    /// ist der Ortler?" nicht.
    /// </para>
    /// </summary>
    public static class FragenAehnlichkeit
    {
        public const double Schwelle = 0.7;

        /// <summary>Eine vorhandene Frage, mit der verglichen wird.</summary>
        public sealed record Kandidat(Guid Id, string Fragetext, string Bezeichnung);

        // Durch dieselbe Abflachung wie die Fragen - „für" wird dabei zu dem, was im Text steht.
        private static readonly HashSet<string> Fuellwoerter = new[]
        {
            "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einem", "einer", "eines",
            "und", "oder", "ist", "sind", "war", "wird", "im", "in", "an", "am", "auf", "von", "vom",
            "zu", "zum", "zur", "mit", "für", "the", "a", "an", "of", "is", "welche", "welcher",
            "welches", "wie", "was", "wer", "wo", "wann",
        }.Select(Flach).ToHashSet(StringComparer.Ordinal);

        /// <summary>Die ähnlichsten, bestes zuerst - ohne die Frage selbst.</summary>
        public static IReadOnlyList<(Kandidat Frage, double Wert)> Finde(string? fragetext, IEnumerable<Kandidat> vorhandene, Guid selbst, int hoechstens = 3)
        {
            var eigene = Woerter(fragetext);

            if (eigene.Count == 0)
                return [];

            return vorhandene
                .Where(k => k.Id != selbst)
                .Select(k => (Frage: k, Wert: Wert(eigene, Woerter(k.Fragetext))))
                .Where(t => t.Wert >= Schwelle)
                .OrderByDescending(t => t.Wert)
                .Take(hoechstens)
                .ToList();
        }

        /// <summary>Ähnlichkeit zweier Fragetexte von 0 bis 1.</summary>
        public static double Wert(string? a, string? b) => Wert(Woerter(a), Woerter(b));

        private static double Wert(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0)
                return 0;

            var gemeinsam = a.Count(b.Contains);
            var jaccard = (double)gemeinsam / (a.Count + b.Count - gemeinsam);

            // Steckt die kürzere Frage fast ganz in der längeren („Hauptstadt von Peru?" in „Welche
            // Stadt ist die Hauptstadt von Peru?"), ist es dieselbe - bei einem einzigen Wort nicht.
            var kleiner = Math.Min(a.Count, b.Count);
            var enthalten = kleiner >= 2 ? (double)gemeinsam / kleiner : 0;

            return Math.Max(jaccard, enthalten);
        }

        internal static HashSet<string> Woerter(string? text)
        {
            var woerter = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(text))
                return woerter;

            foreach (var wort in Flach(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Fuellwoerter.Contains(wort) && (wort.Length > 1 || char.IsDigit(wort[0])))
                    woerter.Add(wort);
            }

            return woerter;
        }

        /// <summary>Kleinschreibung, Umlaute ausgeschrieben, Akzente weg, Satzzeichen zu Leerzeichen.</summary>
        private static string Flach(string text)
        {
            var flach = new StringBuilder();
            foreach (var c in text.ToLowerInvariant().Replace("ß", "ss").Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                flach.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }

            return flach.ToString().Trim();
        }
    }
}
