namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Ein Stand im Tippsprint, gemeldet vom Telefon.</summary>
    /// <param name="Tipps">So oft hat der Spieler bisher getippt.</param>
    /// <param name="Millisekunden">So lange laeuft sein Sprint, gemessen am Telefon.</param>
    /// <param name="Ende">Der Endstand - danach nimmt der Server nichts mehr an.</param>
    public sealed record TippsprintStand(int Tipps, int Millisekunden, bool Ende);

    /// <summary>Was der Tippsprint ergeben hat.</summary>
    /// <param name="Endstaende">Je Spieler sein Endstand - wer fehlt, hat nichts abgegeben.</param>
    /// <param name="Gewinner">Die Spieler mit den meisten Tipps; leer, wenn keiner getippt hat.</param>
    public sealed record TippsprintErgebnis(IReadOnlyDictionary<Guid, int> Endstaende, IReadOnlySet<Guid> Gewinner)
    {
        public bool IstGewinner(Guid spieler) => Gewinner.Contains(spieler);

        public int? TippsVon(Guid spieler) => Endstaende.TryGetValue(spieler, out var tipps) ? tipps : null;
    }

    /// <summary>
    /// Der Tippsprint (M35 C1): Plausibilitaet der Staende und die Wertung.
    /// <para>
    /// <b>Gezaehlt wird am Telefon</b>, der Server prueft nur: hoechstens
    /// <see cref="MaxTapsPerSecond"/> Tipps je Sekunde, ein Stand sinkt nie, der Endstand liegt nicht
    /// unter dem letzten Zwischenstand. <b>Nur Vorschlaege, nie Minus</b> (E1).
    /// </para>
    /// </summary>
    public static class TippsprintEvaluator
    {
        /// <summary>Mehr schafft kein Finger - darueber ist es ein Fehler oder geschummelt.</summary>
        public const int MaxTapsPerSecond = 20;

        /// <summary>So oft meldet das Telefon seinen Zwischenstand fuers Rennen am Beamer.</summary>
        public const int ReportIntervalMs = 500;

        /// <summary>Ob so viele Tipps in so vielen Millisekunden moeglich sind.</summary>
        public static bool IstPlausibel(int tipps, int millisekunden)
            => tipps >= 0 && millisekunden >= 0 && (long)tipps * 1000 <= (long)MaxTapsPerSecond * millisekunden;

        /// <summary>Prueft einen neuen Stand gegen den bisherigen; <c>null</c> heisst verwerfen.</summary>
        /// <param name="bisher">Der zuletzt angenommene Stand des Spielers, <c>null</c> vor dem ersten.</param>
        /// <param name="neu">Was das Telefon meldet.</param>
        /// <param name="dauerSekunden">Die Dauer des Sprints - laenger misst kein ehrliches Telefon.</param>
        public static TippsprintStand? Pruefe(TippsprintStand? bisher, TippsprintStand neu, int dauerSekunden)
        {
            ArgumentNullException.ThrowIfNull(neu);

            if (bisher?.Ende == true || neu.Millisekunden > dauerSekunden * 1000)
                return null;

            if (!IstPlausibel(neu.Tipps, neu.Millisekunden))
                return null;

            // Nie sinken - und damit auch kein Endstand unter dem letzten Zwischenstand.
            return bisher != null && neu.Tipps < bisher.Tipps ? null : neu;
        }

        /// <summary>Die meisten Tipps gewinnen, bei Gleichstand alle. Null Tipps gewinnen nie.</summary>
        public static TippsprintErgebnis Werte(IReadOnlyDictionary<Guid, int> endstaende)
        {
            ArgumentNullException.ThrowIfNull(endstaende);

            var beste = endstaende.Values.DefaultIfEmpty(0).Max();
            var gewinner = beste > 0
                ? endstaende.Where(e => e.Value == beste).Select(e => e.Key).ToHashSet()
                : new HashSet<Guid>();

            return new TippsprintErgebnis(endstaende, gewinner);
        }

        /// <summary>„1 Tipp", „42 Tipps" - fuer Beamer, Spielerkarte und Uebersicht dieselben Worte.</summary>
        public static string Describe(int tipps) => tipps == 1 ? "1 Tipp" : $"{tipps} Tipps";
    }
}
