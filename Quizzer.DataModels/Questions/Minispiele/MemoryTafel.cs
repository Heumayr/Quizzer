namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Das Memory am Beamer (M35 C5): je Spieler die gefundenen Paare mit Balken, wer fertig ist, mit Zeit.
    /// Die Belegung steht hier nie - am Beamer schauen alle mit.
    /// </summary>
    public static class MemoryTafel
    {
        /// <summary>Der laufende Stand; wer noch nichts umgedreht hat, steht bei 0.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Rennen(
            IReadOnlyDictionary<Guid, MemoryStand> staende, int paareGesamt, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(staende);

            return Zeilen(spieler, staende.GetValueOrDefault, paareGesamt, _ => false, $"0 von {paareGesamt} Paaren");
        }

        /// <summary>Der Endstand in der Rangfolge, die Sieger markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(MemoryErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return Zeilen(spieler, ergebnis.StandVon, ergebnis.PaareGesamt, ergebnis.IstGewinner, "keine Karte umgedreht");
        }

        private static List<ReaktionTafelZeile> Zeilen(
            IEnumerable<(Guid Id, string Name)> spieler, Func<Guid, MemoryStand?> standVon, int paareGesamt,
            Func<Guid, bool> sieger, string ohne)
        {
            var gesamt = Math.Max(1, paareGesamt);

            return spieler
                .Select(s => (s.Id, s.Name, Stand: standVon(s.Id)))
                .OrderBy(z => z.Stand?.Fertig ?? TimeSpan.MaxValue)
                .ThenByDescending(z => z.Stand?.Paare ?? 0)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name,
                    z.Stand is { } stand ? (stand.Fertig != null ? "✓ " : string.Empty) + MemoryEvaluator.Describe(stand, paareGesamt) : ohne,
                    sieger(z.Id),
                    (double)(z.Stand?.Paare ?? 0) / gesamt))
                .ToList();
        }
    }
}
