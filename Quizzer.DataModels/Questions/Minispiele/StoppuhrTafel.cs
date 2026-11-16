namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Stoppuhr am Beamer (M35 C1): je Spieler die gestoppte Zeit und wie weit er daneben lag, die
    /// naechsten zuerst; danach zu spaet, dann keine Abgabe.
    /// </summary>
    public static class StoppuhrTafel
    {
        public static IReadOnlyList<ReaktionTafelZeile> Ende(StoppuhrErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return spieler
                .Select(s => (s.Id, s.Name, Versuch: ergebnis.VersuchVon(s.Id), Abstand: ergebnis.AbstandVon(s.Id)))
                .OrderBy(z => z.Abstand.HasValue ? 0 : z.Versuch != null ? 1 : 2)
                .ThenBy(z => z.Abstand ?? int.MaxValue)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, Text(z.Versuch, z.Abstand), ergebnis.IstGewinner(z.Id)))
                .ToList();
        }

        /// <summary>„10,23 s (0,23 s daneben)" - oder warum es keine Zeit gibt.</summary>
        public static string Text(StoppuhrVersuch? versuch, int? abstand)
            => versuch is { Gewertet: true } && abstand is { } ms
                ? $"{StoppuhrEvaluator.Sekunden(versuch.Millisekunden)} ({StoppuhrEvaluator.DescribeDistance(ms)})"
                : versuch == null ? "keine Abgabe" : "zu spät – keine Wertung";
    }
}
