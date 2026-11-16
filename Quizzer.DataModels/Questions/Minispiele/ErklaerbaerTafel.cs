namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Der Erklaerbaer am Beamer (M35 C6): je Spieler erraten, erklaert und Punkte ueber die gespielten Runden; am Ende
    /// die meisten markiert. Den offenen Begriff kennt die Tafel nie - er steht nur im Kopf, wenn die Runde zu ist.
    /// </summary>
    public static class ErklaerbaerTafel
    {
        /// <param name="ende">Erst am Ende markiert die Tafel den Sieger.</param>
        public static IReadOnlyList<ReaktionTafelZeile> Stand(
            ErklaerbaerErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler, bool ende)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            var gewinner = ergebnis.Gewinner;

            return spieler
                .Select(s => (s.Id, s.Name, Punkte: ergebnis.PunkteVon(s.Id)))
                .OrderByDescending(z => z.Punkte)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, ErklaerbaerEvaluator.Karte(ergebnis, z.Id), ende && gewinner.Contains(z.Id)))
                .ToList();
        }
    }
}
