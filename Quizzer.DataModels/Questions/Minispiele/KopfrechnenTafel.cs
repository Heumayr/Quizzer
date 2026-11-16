namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Das Kopfrechnen am Beamer (M35 C5): je Spieler die richtigen Antworten mit Balken, der Fuehrende voll.
    /// Die Zahl steht immer daneben - der Balken allein waere Information ueber Laenge und Farbe.
    /// </summary>
    public static class KopfrechnenTafel
    {
        /// <summary>Der laufende Stand; wer noch nichts beantwortet hat, steht bei 0.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Rennen(
            IReadOnlyDictionary<Guid, KopfrechnenStand> staende, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(staende);

            return Zeilen(spieler, staende.GetValueOrDefault, _ => false, "0 richtig");
        }

        /// <summary>Der Endstand, die Sieger markiert; ohne Antwort „keine Antwort".</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(
            KopfrechnenErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return Zeilen(spieler, ergebnis.StandVon, ergebnis.IstGewinner, "keine Antwort");
        }

        private static List<ReaktionTafelZeile> Zeilen(
            IEnumerable<(Guid Id, string Name)> spieler, Func<Guid, KopfrechnenStand?> standVon,
            Func<Guid, bool> sieger, string ohne)
        {
            var liste = spieler.Select(s => (s.Id, s.Name, Stand: standVon(s.Id))).ToList();
            var vorn = Math.Max(1, liste.Select(z => z.Stand?.Richtig ?? 0).DefaultIfEmpty(0).Max());

            return liste
                .OrderByDescending(z => z.Stand?.Richtig ?? 0)
                .ThenBy(z => z.Stand?.Beantwortet ?? 0)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name,
                    z.Stand is { } stand ? KopfrechnenEvaluator.Describe(stand) : ohne,
                    sieger(z.Id),
                    (double)(z.Stand?.Richtig ?? 0) / vorn))
                .ToList();
        }
    }
}
