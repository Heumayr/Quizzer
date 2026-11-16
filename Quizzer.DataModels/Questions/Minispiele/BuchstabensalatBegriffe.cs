namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Vergleichen und Verwuerfeln beim Buchstabensalat (M35 C5). Verglichen wird ohne Gross- und
    /// Kleinschreibung, mit ä=ae, ö=oe, ü=ue, ß=ss; Leerzeichen am Anfang und Ende zaehlen nicht, mehrere
    /// in der Mitte wie eines (Annahme C5b: ein doppelt getipptes Leerzeichen soll nicht scheitern).
    /// </summary>
    public static class BuchstabensalatBegriffe
    {
        /// <summary>Die Vergleichsform: klein, Umlaute ausgeschrieben, Leerzeichen gestaucht.</summary>
        public static string Normalisiere(string? text)
        {
            var woerter = (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            return string.Join(' ', woerter).ToLowerInvariant()
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        }

        /// <summary>Ob eine Eingabe den Begriff trifft.</summary>
        public static bool Gleich(string? eingabe, string begriff)
            => Normalisiere(eingabe).Length > 0 && Normalisiere(eingabe) == Normalisiere(begriff);

        /// <summary>
        /// Mischt die Buchstaben des Begriffs (Leerzeichen und Zeichen bleiben stehen) und schreibt sie gross.
        /// <b>Das Wirrwarr gleicht nie dem Begriff</b>, auch nicht in der Vergleichsform - sonst stuende die
        /// Loesung am Telefon. Geht das nicht (ein Buchstabe, lauter gleiche), kommt <c>null</c>.
        /// </summary>
        /// <param name="zufall">Wie <see cref="Random.Next(int, int)"/>.</param>
        public static string? Verwuerfle(string begriff, Func<int, int, int> zufall)
        {
            ArgumentNullException.ThrowIfNull(zufall);

            var zeichen = (begriff ?? string.Empty).Trim().ToCharArray();
            var stellen = Enumerable.Range(0, zeichen.Length).Where(i => char.IsLetter(zeichen[i])).ToArray();

            if (stellen.Length < 2)
                return null;

            for (var i = stellen.Length - 1; i > 0; i--)
            {
                var j = Math.Clamp(zufall(0, i + 1), 0, i);
                (zeichen[stellen[i]], zeichen[stellen[j]]) = (zeichen[stellen[j]], zeichen[stellen[i]]);
            }

            if (!Gleich(new string(zeichen), begriff!))
                return Gross(zeichen);

            // Zufaellig wieder der Begriff: der erste Tausch zweier Buchstaben, der ihn veraendert.
            foreach (var a in stellen)
            {
                foreach (var b in stellen.Where(b => b > a))
                {
                    (zeichen[a], zeichen[b]) = (zeichen[b], zeichen[a]);

                    if (!Gleich(new string(zeichen), begriff!))
                        return Gross(zeichen);

                    (zeichen[a], zeichen[b]) = (zeichen[b], zeichen[a]);
                }
            }

            return null;
        }

        /// <summary>Ob sich der Begriff verwuerfeln laesst - sonst wird er nicht gespielt und zaehlt nicht als Begriff (V2).</summary>
        public static bool Verwuerfelbar(string? begriff) => Verwuerfle(begriff ?? string.Empty, (von, _) => von) != null;

        private static string Gross(char[] zeichen) => new string(zeichen).ToUpperInvariant();
    }
}
