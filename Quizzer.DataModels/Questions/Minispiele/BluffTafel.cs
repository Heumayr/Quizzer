namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Der Bluff am Beamer (M35 C6). Waehrend einer Phase nur, wer abgegeben hat - nie welcher Bluff und nie welche Wahl.
    /// Am Ende je Spieler gefunden, hereingelegt und Punkte; die meisten markiert.
    /// </summary>
    public static class BluffTafel
    {
        public static IReadOnlyList<ReaktionTafelZeile> Eingang(
            IReadOnlySet<Guid> abgegeben, int phase, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(abgegeben);
            ArgumentNullException.ThrowIfNull(spieler);

            var erledigt = phase >= 2 ? "✓ gewählt" : "✓ Bluff abgegeben";

            return spieler
                .OrderBy(s => s.Name, StringComparer.CurrentCulture)
                .Select(s => new ReaktionTafelZeile(s.Name, abgegeben.Contains(s.Id) ? erledigt : "…", false))
                .ToList();
        }

        public static IReadOnlyList<ReaktionTafelZeile> Ende(BluffErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            var gewinner = ergebnis.Gewinner;

            return spieler
                .Select(s => (s.Id, s.Name, Punkte: ergebnis.PunkteVon(s.Id)))
                .OrderByDescending(z => z.Punkte)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, BluffEvaluator.Karte(ergebnis, z.Id), gewinner.Contains(z.Id)))
                .ToList();
        }
    }
}
