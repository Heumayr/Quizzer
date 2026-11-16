using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>
    /// Sonderregel Experte (M35 C3b): jeder waehlt, wer fuer alle antwortet - nie sich selbst. Der Meistgewaehlte
    /// antwortet allein (Gleichstand: der erste in der Spielerreihenfolge, der Spielleiter kann umwaehlen). Ist er
    /// richtig, bekommt er wie immer die Feldpunkte und jeder, der ihn gewaehlt hat, den Anteil dazu.
    /// </summary>
    public static class ExperteRegel
    {
        /// <summary>Die Meistgewaehlten in Spielerreihenfolge; leer, wenn niemand waehlt.</summary>
        /// <param name="wahlen">Je Waehler, wen er gewaehlt hat.</param>
        /// <param name="reihenfolge">Die Spieler in der Reihenfolge der Karten - entscheidet die Vorgabe beim Gleichstand.</param>
        public static IReadOnlyList<Guid> Kandidaten(IReadOnlyDictionary<Guid, Guid> wahlen, IReadOnlyList<Guid> reihenfolge)
        {
            ArgumentNullException.ThrowIfNull(wahlen);
            ArgumentNullException.ThrowIfNull(reihenfolge);

            if (wahlen.Count == 0)
                return [];

            var stimmen = wahlen.Values.GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
            var meiste = stimmen.Values.Max();
            var platz = reihenfolge.Select((id, i) => (id, i)).ToDictionary(e => e.id, e => e.i);

            return stimmen.Where(s => s.Value == meiste)
                .Select(s => s.Key)
                .OrderBy(id => platz.GetValueOrDefault(id, int.MaxValue))
                .ToList();
        }

        /// <summary>
        /// Die Vorschlaege fuer die Waehler des Experten: ist er richtig, die eigene Bewertung plus der Bonus. Der
        /// Experte selbst und alle anderen spielen wie immer - fuer sie kein Eintrag.
        /// </summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> Werte(
            Guid experte, IReadOnlyDictionary<Guid, Guid> wahlen, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge, int bonus)
        {
            ArgumentNullException.ThrowIfNull(wahlen);
            ArgumentNullException.ThrowIfNull(eingaenge);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            if (eingaenge.GetValueOrDefault(experte)?.Bewertung != RatingKind.Right || bonus <= 0)
                return vorschlaege;

            foreach (var (waehler, gewaehlt) in wahlen)
            {
                if (gewaehlt == experte && eingaenge.TryGetValue(waehler, out var eigener))
                    vorschlaege[waehler] = Sonderregelwertung.Gekappt(eigener.Grundwert + bonus, $"Experte richtig gewählt ({Punkteanteil.Text(bonus)})", eigener.Konto);
            }

            return vorschlaege;
        }
    }
}
