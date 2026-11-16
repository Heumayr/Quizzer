using System.Globalization;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Wie weit ein Spieler beim Memory ist.</summary>
    /// <param name="Paare">So viele Paare hat er gefunden.</param>
    /// <param name="Fertig">Wann er das letzte fand, an der Serveruhr seit dem Start; <c>null</c>, solange er nicht fertig ist.</param>
    public sealed record MemoryStand(int Paare, TimeSpan? Fertig);

    /// <summary>Ein angenommenes Umdrehen, gemeldet an den Spielleiter - ohne Motiv.</summary>
    /// <param name="Karte">Welche Karte.</param>
    /// <param name="Paar">Ob damit ein Paar gefunden ist.</param>
    /// <param name="Stand">Der Stand des Spielers danach.</param>
    public sealed record MemoryZug(int Karte, bool Paar, MemoryStand Stand);

    /// <summary>Was das Memory ergeben hat.</summary>
    /// <param name="Staende">Je Spieler, der eine Karte umgedreht hat, sein Stand.</param>
    /// <param name="PaareGesamt">So viele Paare hat das Feld.</param>
    /// <param name="Rangfolge">Die Spieler mit Stand, vorn der beste: erst nach Zeit, bei Gleichstand nach Paaren.</param>
    /// <param name="Gewinner">Die vorn; leer, wenn keiner ein Paar hat.</param>
    /// <param name="Vorschlaege">Die Punkte der Gewinner, mit Grund (E1).</param>
    public sealed record MemoryErgebnis(
        IReadOnlyDictionary<Guid, MemoryStand> Staende, int PaareGesamt, IReadOnlyList<Guid> Rangfolge,
        IReadOnlySet<Guid> Gewinner, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public bool IstGewinner(Guid spieler) => Gewinner.Contains(spieler);

        public MemoryStand? StandVon(Guid spieler) => Staende.GetValueOrDefault(spieler);
    }

    /// <summary>
    /// Das Memory (M35 C5): wer zuerst alle Paare hat, gemessen an der Serveruhr, gewinnt die Feldpunkte.
    /// Laeuft die Zeit vorher ab, gewinnt, wer die meisten Paare hat; bei Gleichstand alle.
    /// </summary>
    public static class MemoryEvaluator
    {
        /// <summary>
        /// Plausibilitaetsgrenze am Server: zwei Karten tippt ein Mensch schnell hintereinander, dann muss er
        /// hinsehen - mehr als fuenf in einer Sekunde ist ein Skript, das das Feld abtastet.
        /// </summary>
        public const int MaxUmdrehenJeSekunde = 5;

        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Der Rang: wer fertig ist, nach seiner Zeit; danach die uebrigen nach Paaren.</summary>
        private static (TimeSpan Zeit, int Paare) Rang(MemoryStand stand) => (stand.Fertig ?? TimeSpan.MaxValue, -stand.Paare);

        public static IReadOnlyList<Guid> Rangfolge(IReadOnlyDictionary<Guid, MemoryStand> staende)
        {
            ArgumentNullException.ThrowIfNull(staende);

            return staende.OrderBy(s => Rang(s.Value)).Select(s => s.Key).ToList();
        }

        /// <summary>Alle mit dem besten Rang; wer kein einziges Paar hat, gewinnt nie.</summary>
        public static IReadOnlySet<Guid> Gewinner(IReadOnlyDictionary<Guid, MemoryStand> staende)
        {
            var rangfolge = Rangfolge(staende);

            if (rangfolge.Count == 0 || staende[rangfolge[0]].Paare == 0)
                return new HashSet<Guid>();

            var bester = Rang(staende[rangfolge[0]]);
            return rangfolge.Where(id => Rang(staende[id]) == bester).ToHashSet();
        }

        /// <summary>Die Wertung: jeder Gewinner bekommt die Feldpunkte als Vorschlag mit Grund; ein Plus wird nie gekappt (E2).</summary>
        public static MemoryErgebnis Werte(
            IReadOnlyDictionary<Guid, MemoryStand> staende, int paareGesamt, int feldpunkte, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(konten);

            var gewinner = Gewinner(staende);
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var id in gewinner)
            {
                var punkte = konten.Kappe(id, feldpunkte);

                if (punkte != 0)
                    vorschlaege[id] = new(punkte, $"Memory: {Describe(staende[id], paareGesamt)} – "
                        + (staende[id].Fertig != null ? "am schnellsten " : "die meisten ") + Punkteanteil.Text(punkte));
            }

            return new(staende, paareGesamt, Rangfolge(staende), gewinner, vorschlaege);
        }

        /// <summary>„42,3 s".</summary>
        public static string Sekunden(TimeSpan zeit) => zeit.TotalSeconds.ToString("0.0", Deutsch) + " s";

        /// <summary>„alle 6 Paare in 42,3 s" oder „3 von 6 Paaren" - fuer Beamer, Karte und Buzzer-Fenster dieselben Worte.</summary>
        public static string Describe(MemoryStand stand, int paareGesamt)
        {
            ArgumentNullException.ThrowIfNull(stand);

            return stand.Fertig is { } zeit
                ? $"alle {paareGesamt} Paare in {Sekunden(zeit)}"
                : $"{stand.Paare} von {paareGesamt} Paaren";
        }

        /// <summary>Der Satz auf der Spielerkarte.</summary>
        public static string Describe(MemoryErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            if (ergebnis.StandVon(spieler) is not { } stand)
                return "Keine Karte umgedreht – von Hand werten";

            var satz = Describe(stand, ergebnis.PaareGesamt);
            return ergebnis.IstGewinner(spieler) ? satz + " – vorn" : satz;
        }
    }
}
