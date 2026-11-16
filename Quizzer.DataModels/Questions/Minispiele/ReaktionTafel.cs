namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Zeile der Tafel am Beamer - Reaktion, Tippsprint und Stoppuhr (M35 C1) teilen sie.</summary>
    /// <param name="Name">Der Spieler.</param>
    /// <param name="Zeit">Sein Wert im Klartext oder warum er keinen hat.</param>
    /// <param name="Sieger">Ob er gewonnen hat - am Beamer mit Symbol und Text, nie nur Farbe.</param>
    /// <param name="Balken">Anteil 0..1 fuer das Rennen des Tippsprints; <c>null</c> = kein Balken.</param>
    public sealed record ReaktionTafelZeile(string Name, string Zeit, bool Sieger, double? Balken = null)
    {
        public string SiegerText => Sieger ? "★ Sieger" : string.Empty;

        public bool HatBalken => Balken.HasValue;

        public double BalkenAnteil => Math.Clamp(Balken ?? 0, 0, 1);
    }

    /// <summary>
    /// Die Zeiten der Reaktion fuer den Beamer (M35 B3): nach jeder Runde die Zeiten der Runde, am Ende
    /// die Bestzeiten. Gueltige Zeiten aufsteigend, danach Fruehstart, zu langsam, keine Abgabe.
    /// </summary>
    public static class ReaktionTafel
    {
        /// <summary>Die Zeiten einer Runde; wer fehlt, steht mit „keine Abgabe" da.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Runde(
            IReadOnlyDictionary<Guid, ReactionAttempt> runde, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(runde);

            return spieler
                .Select(s => (s.Name, Versuch: runde.GetValueOrDefault(s.Id)))
                .OrderBy(z => Rang(z.Versuch))
                .ThenBy(z => z.Versuch?.Milliseconds ?? int.MaxValue)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, ReaktionEvaluator.Describe(z.Versuch), false))
                .ToList();
        }

        /// <summary>Die Bestzeiten ueber alle Runden, die Sieger markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(ReaktionErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return spieler
                .Select(s => (s.Id, s.Name, Bestzeit: ergebnis.BestzeitVon(s.Id)))
                .OrderBy(z => z.Bestzeit ?? int.MaxValue)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name,
                    z.Bestzeit is { } ms ? $"{ms} ms"
                        : ergebnis.Abgegeben.Contains(z.Id) ? "keine gültige Zeit" : "keine Abgabe",
                    ergebnis.IstGewinner(z.Id)))
                .ToList();
        }

        private static int Rang(ReactionAttempt? versuch) => versuch?.Outcome switch
        {
            ReactionOutcome.Valid => 0,
            ReactionOutcome.FalseStart => 1,
            ReactionOutcome.TooSlow => 2,
            _ => 3,
        };
    }
}
