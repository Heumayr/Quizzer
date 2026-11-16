namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Aufloesung der Spiele mit Spielerwahl am Beamer (M35 C2): wer wen gewaehlt hat und was daraus
    /// wurde, je Spieler eine Zeile mit Text. Dieselbe Zeile wie bei der Reaktion, also dieselbe Ansicht.
    /// </summary>
    public static class SpielerwahlTafel
    {
        /// <summary>Eine Runde Schere-Stein-Papier: beide Zuege, der Rundensieger markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> DuellRunde(
            Guid a, Guid b, IReadOnlyDictionary<Guid, SchereSteinPapier> zuege, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(zuege);
            ArgumentNullException.ThrowIfNull(name);

            var sieger = DuellEvaluator.RundenSieger(a, b, zuege);

            return new[] { a, b }
                .Select(id => new ReaktionTafelZeile(
                    name(id),
                    DuellEvaluator.Describe(zuege.TryGetValue(id, out var zug) ? zug : null),
                    sieger == id))
                .ToList();
        }

        /// <summary>Der Stand des Duells nach Siegen; am Ende der Sieger markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> DuellStand(DuellErgebnis ergebnis, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(name);

            return new[] { ergebnis.Herausforderer, ergebnis.Gegner }
                .Select(id => (Id: id, Siege: ergebnis.SiegeVon(id)))
                .OrderByDescending(z => z.Siege)
                .Select(z => new ReaktionTafelZeile(name(z.Id), z.Siege == 1 ? "1 Sieg" : $"{z.Siege} Siege", ergebnis.Sieger == z.Id))
                .ToList();
        }

        /// <summary>Raeuber und Waechter: jeder mit seiner Wahl und seiner Summe, die meisten Punkte oben.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Raeuber(
            RaeuberErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            var liste = spieler.ToList();
            string Name(Guid id) => liste.FirstOrDefault(s => s.Id == id).Name ?? "?";

            return liste
                .Select(s => (s.Name, Text: RaeuberEvaluator.Describe(ergebnis, s.Id, Name), Vorschlag: ergebnis.Vorschlaege.GetValueOrDefault(s.Id)))
                .OrderByDescending(z => z.Vorschlag?.Punkte ?? 0)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(
                    z.Name,
                    z.Vorschlag is { } v ? $"{z.Text} ({Punkteanteil.Text(v.Punkte)})" : z.Text,
                    false))
                .ToList();
        }

        /// <summary>Teilen oder Nehmen: beide Wahlen, wer etwas bekommt, ist markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> TeilenNehmen(
            TeilenNehmenPaar paar, TeilenNehmenErgebnis ergebnis, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(paar);
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(name);

            return new[] { (paar.A, paar.NimmtA), (paar.B, paar.NimmtB) }
                .Select(s => (Id: s.Item1, Text: TeilenNehmenEvaluator.Describe(s.Item2), Vorschlag: ergebnis.Vorschlaege.GetValueOrDefault(s.Item1)))
                .Select(z => new ReaktionTafelZeile(
                    name(z.Id),
                    z.Vorschlag is { } v ? $"{z.Text} ({Punkteanteil.Text(v.Punkte)})" : z.Text,
                    z.Vorschlag is { Punkte: > 0 }))
                .ToList();
        }
    }
}
