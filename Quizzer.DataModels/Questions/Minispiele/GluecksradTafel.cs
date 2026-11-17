namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Das Gluecksrad am Beamer (M35 C4): je Feld eine Zeile, das laufende mit Pfeil - nie nur hervorgehoben;
    /// am Ende je Betroffenem, was ihm geschah, mit Punkten.
    /// </summary>
    public static class GluecksradTafel
    {
        /// <summary>Steht vor dem Feld, auf dem das Rad gerade steht.</summary>
        public const string Zeiger = "▶ ";

        /// <summary>Das Rad mit dem markierten Feld; gelandet sagt es die Zeile dazu. Im Teamspiel ohne Robin Hood (M37 T8).</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Rad(WheelSegment? markiert, bool gelandet, bool teamspiel = false)
            => GluecksradEvaluator.FelderFuer(teamspiel)
                .Select(f => new ReaktionTafelZeile(
                    (f == markiert ? Zeiger : string.Empty) + GluecksradEvaluator.Name(f),
                    f == markiert && gelandet
                        ? $"{GluecksradEvaluator.Erklaerung(f)} – hier steht das Rad!"
                        : GluecksradEvaluator.Erklaerung(f),
                    false))
                .ToList();

        /// <summary>Nach der Runde: wer betroffen ist, mit seiner Rolle und seiner Bewegung, das Plus oben.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(GluecksradErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            return spieler
                .Where(s => ergebnis.Vorschlaege.ContainsKey(s.Id))
                .Select(s => (s.Id, s.Name, Punkte: ergebnis.Vorschlaege[s.Id].Punkte))
                .OrderByDescending(z => z.Punkte)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, $"{ergebnis.Describe(z.Id)} {Punkteanteil.Text(z.Punkte)}", false))
                .ToList();
        }
    }
}
