namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Der Buchstabensalat am Beamer (M35 C5): je Spieler die geloesten Begriffe mit Balken, am Ende samt Punkten;
    /// die meisten markiert. Die Zahl steht immer neben dem Balken.
    /// </summary>
    public static class BuchstabensalatTafel
    {
        /// <summary>Der laufende Stand; wer noch nichts geloest hat, steht bei 0.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Rennen(
            IReadOnlyDictionary<Guid, int> geloest, int begriffe, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(geloest);
            ArgumentNullException.ThrowIfNull(spieler);

            return spieler
                .Select(s => (s.Name, Zahl: geloest.GetValueOrDefault(s.Id)))
                .OrderByDescending(z => z.Zahl)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, BuchstabensalatEvaluator.Describe(new(z.Zahl)), false,
                    begriffe > 0 ? (double)z.Zahl / begriffe : 0))
                .ToList();
        }

        /// <summary>Der Endstand mit Punkten; ohne Versuch „kein Versuch".</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(BuchstabensalatErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            var gewinner = ergebnis.Gewinner;

            return spieler
                .Select(s => (s.Id, s.Name, Stand: ergebnis.StandVon(s.Id)))
                .OrderByDescending(z => z.Stand?.Geloest ?? 0)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name,
                    z.Stand is { } stand
                        ? $"{BuchstabensalatEvaluator.Describe(stand)} · {Punkteanteil.Text(ergebnis.PunkteVon(z.Id))}"
                        : "kein Versuch",
                    gewinner.Contains(z.Id),
                    ergebnis.Begriffe > 0 ? (double)(z.Stand?.Geloest ?? 0) / ergebnis.Begriffe : 0))
                .ToList();
        }
    }
}
