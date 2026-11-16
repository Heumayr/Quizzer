namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Blitzrunde am Beamer (M35 C5). Waehrend der Runde nur der Fortschritt je Spieler - ob eine Antwort
    /// stimmte, wuerde die Wahrheit einer Aussage verraten, die andere noch vor sich haben. Am Ende richtig,
    /// falsch und die Punkte; die Zahl steht immer neben dem Balken.
    /// </summary>
    public static class BlitzrundeTafel
    {
        /// <summary>Der laufende Fortschritt; wer noch nichts beantwortet hat, steht bei 0.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Rennen(
            IReadOnlyDictionary<Guid, int> beantwortet, int aussagen, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(beantwortet);
            ArgumentNullException.ThrowIfNull(spieler);

            return spieler
                .Select(s => (s.Name, Zahl: beantwortet.GetValueOrDefault(s.Id)))
                .OrderByDescending(z => z.Zahl)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, $"{z.Zahl} von {aussagen} beantwortet", false,
                    aussagen > 0 ? (double)z.Zahl / aussagen : 0))
                .ToList();
        }

        /// <summary>Der Endstand: die meisten Punkte vorn und markiert, sofern sie ueber null liegen.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(BlitzrundeErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            var liste = spieler.Select(s => (s.Id, s.Name, Stand: ergebnis.StandVon(s.Id), Punkte: ergebnis.PunkteVon(s.Id))).ToList();
            var beste = liste.Select(z => z.Punkte).DefaultIfEmpty(0).Max();

            return liste
                .OrderByDescending(z => z.Punkte)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name,
                    z.Stand is { } stand ? $"{BlitzrundeEvaluator.Describe(stand)} · {Punkteanteil.Text(z.Punkte)}" : "keine Antwort",
                    beste > 0 && z.Punkte == beste,
                    ergebnis.Aussagen > 0 ? Math.Max(0, (double)(z.Stand?.Netto ?? 0) / ergebnis.Aussagen) : 0))
                .ToList();
        }

        /// <summary>Nach der Runde: jede Aussage mit ihrer Wahrheit als Wort und Symbol, in der Reihenfolge der Frage.</summary>
        public static IReadOnlyList<string> Aufloesung(IEnumerable<MinispielZeile> aussagen)
        {
            ArgumentNullException.ThrowIfNull(aussagen);

            return aussagen.Select(a => $"{(a.Wahr ? "✓ wahr" : "✗ falsch")} – {a.Text}").ToList();
        }
    }
}
