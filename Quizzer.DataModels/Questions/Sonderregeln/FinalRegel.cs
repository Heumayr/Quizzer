using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Wer in der Auflösung des Finales steht (M37 E4): ein Spieler oder ein Team, mit dem Stand beim Öffnen.</summary>
    public sealed record FinalPlatz(Guid Id, string Name, int Stand);

    /// <summary>
    /// Sonderregel Finale (M37 E4, E01): jeder setzt verdeckt von seinem Kontostand - die Obergrenze ist die des Einsatzes
    /// (<see cref="EinsatzRegel.Obergrenze"/>) -, und wer nichts setzt, setzt 0. Richtig gewinnt den eigenen Einsatz, falsch
    /// verliert ihn, gekappt nach E2. Aufgelöst wird vom Letzten zum Ersten.
    /// </summary>
    public static class FinalRegel
    {
        /// <summary>Der Einsatz dessen, der nichts gesetzt hat - anders als beim Einsatz zählen nicht die Feldpunkte.</summary>
        public const int OhneEinsatz = 0;

        /// <summary>Der Einsatz, mit dem gerechnet wird.</summary>
        public static int EinsatzOder0(int? einsatz) => einsatz ?? OhneEinsatz;

        /// <summary>Der Vorschlag für die eigene Karte; <c>null</c>, wenn nicht bewertet.</summary>
        public static Punktevorschlag? Werte(int? einsatz, SonderregelEingang eingang)
            => EinsatzRegel.Werte(EinsatzOder0(einsatz), eingang);

        /// <summary>
        /// Die Reihenfolge der Auflösung: aufsteigend nach Stand, der Führende zuletzt; bei gleichem Stand nach Name, dann
        /// nach Kennung - am Beamer und beim Spielleiter dieselbe.
        /// </summary>
        public static IReadOnlyList<FinalPlatz> Reihenfolge(IEnumerable<FinalPlatz> plaetze)
        {
            ArgumentNullException.ThrowIfNull(plaetze);

            return plaetze
                .OrderBy(p => p.Stand)
                .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(p => p.Id)
                .ToList();
        }

        /// <summary>„„Ottawa““ - die Antwort am Beamer; ohne eine „keine Antwort“.</summary>
        public static string AntwortText(string? antwort)
            => string.IsNullOrWhiteSpace(antwort) ? "keine Antwort" : $"„{antwort.Trim()}“";
    }
}
