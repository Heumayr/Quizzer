using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Eine Wette: auf wen, was sie kostet und was sie bei einem Treffer bringt.</summary>
    /// <param name="Auf">Der Spieler, auf den gewettet wurde - auch man selbst.</param>
    /// <param name="Name">Sein Name fuer den Grund auf der Karte.</param>
    /// <param name="Einsatz">Was eine verlorene Wette kostet.</param>
    /// <param name="Gewinn">Was eine gewonnene bringt - Einsatz mal Quote, kaufmaennisch gerundet.</param>
    public sealed record Wette(Guid Auf, string Name, int Einsatz, int Gewinn);

    /// <summary>
    /// Sonderregel Wette (M35 C3b): jeder wettet auf einen Spieler, auch auf sich. Einsatz = Anteil der Feldpunkte.
    /// Nur „richtig" des Gewaehlten gewinnt (ein Teilwert zaehlt als verloren); die Quote 1 + Rang/Anzahl folgt dem
    /// Wettkampf-Rang beim Oeffnen - der Letzte bringt am meisten. Die eigene Bewertung zaehlt dazu, die Summe wird
    /// einmal gekappt (E2).
    /// </summary>
    public static class WetteRegel
    {
        public static int Einsatz(int feldpunkte, int anteil) => Punkteanteil.Von(feldpunkte, anteil);

        /// <summary>Wettkampf-Rang nach Kontostand: 1 = vorn, Gleichauf teilen den besseren Platz (1, 2, 2, 4).</summary>
        public static int Rang(Guid spieler, IReadOnlyDictionary<Guid, int> kontostaende)
        {
            ArgumentNullException.ThrowIfNull(kontostaende);

            var eigener = kontostaende.GetValueOrDefault(spieler);
            return 1 + kontostaende.Values.Count(k => k > eigener);
        }

        /// <summary>Einsatz mal (1 + Rang/Anzahl), kaufmaennisch gerundet.</summary>
        public static int Gewinn(int einsatz, int rang, int anzahl)
            => anzahl <= 0 ? einsatz : (int)Math.Round((decimal)einsatz * (anzahl + rang) / anzahl, MidpointRounding.AwayFromZero);

        /// <summary>Die Wette auf diesen Spieler - Rang und Anzahl aus den Kontostaenden beim Oeffnen.</summary>
        public static Wette Auf(Guid spieler, string name, int einsatz, IReadOnlyDictionary<Guid, int> kontostaende)
            => new(spieler, name, einsatz, Gewinn(einsatz, Rang(spieler, kontostaende), kontostaende.Count));

        /// <summary>Der Vorschlag fuer den Wettenden: eigener Grundwert plus Wette, insgesamt gekappt (E2).</summary>
        /// <param name="wette">Seine Wette.</param>
        /// <param name="bewertungDesGewaehlten">Wie der Gewaehlte bewertet ist - nur richtig gewinnt.</param>
        /// <param name="eigener">Die eigene Bewertung des Wettenden.</param>
        public static Punktevorschlag Werte(Wette wette, RatingKind bewertungDesGewaehlten, SonderregelEingang eigener)
        {
            ArgumentNullException.ThrowIfNull(wette);
            ArgumentNullException.ThrowIfNull(eigener);

            var getroffen = bewertungDesGewaehlten == RatingKind.Right;
            var bewegung = getroffen ? wette.Gewinn : -wette.Einsatz;
            var grund = $"Wette auf {wette.Name} {(getroffen ? "gewonnen" : "verloren")} ({Punkteanteil.Text(bewegung)})";

            return Sonderregelwertung.Gekappt(eigener.Grundwert + bewegung, grund, eigener.Konto);
        }
    }
}
