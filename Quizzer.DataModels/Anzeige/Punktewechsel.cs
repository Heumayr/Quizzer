namespace Quizzer.DataModels.Anzeige
{
    /// <summary>
    /// Was der Beamer nach einer Frage über die Punkte sagt (Nutzerwunsch 2026-10-21: „wenn
    /// frage beendet wird sollen die geänderten Punkte minus und plus für ein paar Sekunden
    /// eingeblendet werden ... damit jeder die Änderungen nachvollziehen kann").
    /// <para>
    /// <b>WPF-frei</b>, wie <see cref="Questions.QuestionValidator"/>: Text und Zwischenstände
    /// lassen sich so ohne Oberflächen-Thread prüfen; die Ansicht stellt nur noch dar.
    /// </para>
    /// </summary>
    public static class Punktewechsel
    {
        /// <summary>Wie lange eine Änderung stehen bleibt.</summary>
        public static readonly TimeSpan Anzeigedauer = TimeSpan.FromSeconds(5);

        /// <summary>Wie lange die Zahl zum neuen Stand zählt.</summary>
        public static readonly TimeSpan Zaehldauer = TimeSpan.FromMilliseconds(1200);

        /// <summary>
        /// Die Änderung als Text: „+200", „−100" - mit echtem Minuszeichen, das auf dem Beamer
        /// so breit ist wie das Plus. Ohne Änderung leer.
        /// </summary>
        public static string Text(int delta) => delta switch
        {
            > 0 => $"+{delta}",
            < 0 => $"−{-delta}",
            _ => string.Empty,
        };

        /// <summary>
        /// Der Stand beim Hochzählen nach dem Anteil <paramref name="fortschritt"/> (0 bis 1) der
        /// <see cref="Zaehldauer"/>. Am Anfang schnell, am Ende langsam (kubisch auslaufend) - so
        /// bleibt der Blick an der Zahl, bis sie steht.
        /// </summary>
        public static int Zwischenstand(int alt, int neu, double fortschritt)
        {
            var t = Math.Clamp(fortschritt, 0, 1);
            var weich = 1 - Math.Pow(1 - t, 3);

            return t >= 1 ? neu : alt + (int)Math.Round((neu - alt) * weich, MidpointRounding.AwayFromZero);
        }
    }
}
