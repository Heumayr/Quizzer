using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Was „Alle oder keiner" einem Team bringt.</summary>
    /// <param name="Bewertung">Richtig: alle richtig; falsch: einer nicht; <see cref="RatingKind.None"/>: noch nicht alle bewertet.</param>
    /// <param name="Anteile">Je Mitglied sein Anteil am Feldwert bzw. am Minus; leer, solange offen.</param>
    /// <param name="Offen">Wie viele, die antworten müssen, noch unbewertet sind.</param>
    public sealed record AlleOderKeinerErgebnis(RatingKind Bewertung, IReadOnlyDictionary<Guid, int> Anteile, int Offen);

    /// <summary>
    /// „Alle oder keiner" (M37 T6, Entwurf Abschnitt 3): jedes Mitglied antwortet selbst, die Spielleitung bewertet jede Karte.
    /// Sind alle richtig, die antworten konnten (nicht per Joker gesperrt, nicht getrennt), bekommt das Team den Feldwert
    /// aufgeteilt; ist einer falsch oder nur teilweise richtig, verliert es die Minuspunkte einmal, aufgeteilt (F-M37-06).
    /// Aufgeteilt wird über das ganze Team. Solange jemand unbewertet ist, steht nichts fest.
    /// </summary>
    public static class AlleOderKeiner
    {
        /// <summary>
        /// Die Wertung eines Teams; <c>null</c>, wenn niemand antworten konnte - dann gilt weder „alle richtig" noch ein Minus
        /// (fail-closed: eine leere Menge ist nicht „alle richtig").
        /// </summary>
        /// <param name="feldwert">Was „richtig" bringt.</param>
        /// <param name="minuspunkte">Was „falsch" kostet, als positive Zahl.</param>
        /// <param name="folge">Das Team in Sprecherfolge - aufgeteilt wird über alle, der Rest geht nach vorn.</param>
        /// <param name="ausgelassen">Wer nicht antworten konnte; seine Bewertung zählt nicht.</param>
        /// <param name="bewertungen">Je Mitglied die Bewertung seiner Karte; wer fehlt, ist unbewertet.</param>
        public static AlleOderKeinerErgebnis? Werte(
            int feldwert, int minuspunkte, IReadOnlyList<Guid> folge, IReadOnlySet<Guid> ausgelassen, IReadOnlyDictionary<Guid, RatingKind> bewertungen)
        {
            ArgumentNullException.ThrowIfNull(folge);
            ArgumentNullException.ThrowIfNull(ausgelassen);
            ArgumentNullException.ThrowIfNull(bewertungen);

            var mitglieder = folge.Distinct().ToList();
            var pflichtig = mitglieder.Where(m => !ausgelassen.Contains(m)).Select(m => bewertungen.GetValueOrDefault(m)).ToList();

            if (pflichtig.Count == 0)
                return null;

            if (pflichtig.Any(b => b is RatingKind.Wrong or RatingKind.Partial))
                return new(RatingKind.Wrong, Aufteilung.Verteile(-Math.Abs(minuspunkte), mitglieder), 0);

            var offen = pflichtig.Count(b => b != RatingKind.Right);

            return offen > 0
                ? new(RatingKind.None, new Dictionary<Guid, int>(), offen)
                : new(RatingKind.Right, Aufteilung.Verteile(feldwert, mitglieder), 0);
        }
    }
}
