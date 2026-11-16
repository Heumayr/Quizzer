namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Das Rennen des Tippsprints am Beamer (M35 C1): je Spieler Zahl und Balken, der Fuehrende voll.
    /// Die Zahl steht immer daneben - der Balken allein waere Information ueber Laenge und Farbe.
    /// </summary>
    public static class TippsprintTafel
    {
        /// <summary>Der laufende Stand aus den Zwischenstaenden; wer noch nichts gemeldet hat, steht bei 0.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Rennen(
            IReadOnlyDictionary<Guid, TippsprintStand> staende, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(staende);

            return Zeilen(spieler, id => staende.TryGetValue(id, out var s) ? s.Tipps : 0, _ => false, _ => null);
        }

        /// <summary>Die Endstaende, die Sieger markiert; ohne Abgabe „keine Abgabe".</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(TippsprintErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return Zeilen(spieler, id => ergebnis.TippsVon(id) ?? 0, ergebnis.IstGewinner,
                id => ergebnis.TippsVon(id) == null ? "keine Abgabe" : null);
        }

        private static List<ReaktionTafelZeile> Zeilen(
            IEnumerable<(Guid Id, string Name)> spieler, Func<Guid, int> tippsVon, Func<Guid, bool> sieger,
            Func<Guid, string?> stattZahl)
        {
            var liste = spieler.Select(s => (s.Id, s.Name, Tipps: tippsVon(s.Id))).ToList();
            var vorn = Math.Max(1, liste.Select(z => z.Tipps).DefaultIfEmpty(0).Max());

            return liste
                .OrderByDescending(z => z.Tipps)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name, stattZahl(z.Id) ?? TippsprintEvaluator.Describe(z.Tipps), sieger(z.Id), (double)z.Tipps / vorn))
                .ToList();
        }
    }
}
