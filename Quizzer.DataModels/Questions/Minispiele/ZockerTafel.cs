namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Der Zocker am Beamer (M35 C4): oben der Multiplikator, darunter je Teilnehmer, ob er noch drin ist
    /// oder bei welchem Wert er ausgestiegen ist. Immer als Text - nie allein ueber Farbe.
    /// </summary>
    public static class ZockerTafel
    {
        /// <summary>Die Kopfzeile waehrend der Runde; der Absturzpunkt steht erst da, wenn es abgestuerzt ist.</summary>
        /// <param name="multiplikator">Der Stand an der Serveruhr, in Hundertsteln.</param>
        /// <param name="gestartet">Ob der Vorlauf vorbei ist.</param>
        /// <param name="abgestuerzt">Ob die Serveruhr den Absturz schon erreicht hat.</param>
        public static string Kopfzeile(int multiplikator, bool gestartet, bool abgestuerzt)
            => abgestuerzt ? $"Zocker – ABGESTÜRZT bei {ZockerEvaluator.Text(multiplikator)}!"
                : gestartet ? $"Zocker – {ZockerEvaluator.Text(multiplikator)} und es steigt …"
                : "Zocker – gleich steigt der Multiplikator. Steigt rechtzeitig aus!";

        /// <summary>Waehrend der Runde: wer noch drin ist und wer wo ausgestiegen ist.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Laufend(
            IReadOnlyDictionary<Guid, int> ausstiege, IReadOnlyCollection<Guid> teilnehmer, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ausstiege);
            ArgumentNullException.ThrowIfNull(teilnehmer);
            ArgumentNullException.ThrowIfNull(spieler);

            return spieler
                .Where(s => teilnehmer.Contains(s.Id))
                .Select(s => (s.Name, Wert: ausstiege.TryGetValue(s.Id, out var m) ? m : (int?)null))
                .OrderByDescending(z => z.Wert.HasValue)
                .ThenByDescending(z => z.Wert)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, z.Wert is { } m ? $"bei {ZockerEvaluator.Text(m)} ausgestiegen" : "noch drin", false))
                .ToList();
        }

        /// <summary>Nach der Runde: je Teilnehmer, was er getan hat und was es bringt; der hoechste Ausstieg markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(ZockerErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            return spieler
                .Where(s => ergebnis.Teilnehmer.Contains(s.Id))
                .Select(s => (s.Id, s.Name, Wert: ergebnis.Ausstiege.TryGetValue(s.Id, out var m) ? m : -1))
                .OrderByDescending(z => z.Wert)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, Zeile(ergebnis, z.Id), ergebnis.IstGewinner(z.Id)))
                .ToList();
        }

        private static string Zeile(ZockerErgebnis ergebnis, Guid id)
        {
            var punkte = ergebnis.Vorschlaege.GetValueOrDefault(id)?.Punkte ?? 0;
            return $"{ZockerEvaluator.Describe(ergebnis, id)} {Punkteanteil.Text(punkte)}";
        }
    }
}
