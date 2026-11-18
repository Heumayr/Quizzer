namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// „Richtig beantwortet“ folgt der Bewertung (Nebenbei zu R4-1, Durchsicht M37): „richtig“ und „falsch“ setzen es sofort, schon
    /// als Vorschlag. Wird der Vorschlag ohne Buchung verworfen - Runde wiederholt, zurückgesetzt, „nicht bewertet“ -, gilt wieder
    /// der gebuchte Stand; sonst buchte das nächste Speichern den Sieger einer verworfenen Runde als „richtig beantwortet“.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Was zuletzt gebucht war - beim ersten Wechsel der Bewertung aus dem Ergebnis, danach nach jeder Buchung.</summary>
        private bool? gebuchtKorrekt;

        /// <summary>Was in der Datenbank steht - gemerkt beim Öffnen der Zelle und nach jedem gelungenen Speichern (P10-1).</summary>
        private bool? gespeichertKorrekt;

        /// <summary>
        /// Das Häkchen weicht vom gespeicherten Stand ab (P10-1): eine offene Bewertung, die das Schließen schreibt, aber keine
        /// Buchung - es bewegt keine Punkte, und <c>LetzteWertung.HatGebucht</c> sieht davon ab (P8-5).
        /// </summary>
        public bool HaekchenOffen => gespeichertKorrekt is { } gespeichert && gespeichert != Result.CorrectAnswered;

        internal void MerkeGespeichertesHaekchen() => gespeichertKorrekt = Result.CorrectAnswered;

        private bool KorrektNach(ScoreSuggestion bewertung)
        {
            gebuchtKorrekt ??= Result.CorrectAnswered;

            // Beim Buchen räumt SetManipulationToResult den Vorschlag ab - was eben gebucht wurde, ist der neue Stand.
            if (bucht)
                gebuchtKorrekt = Result.CorrectAnswered;

            return bewertung switch
            {
                ScoreSuggestion.Right => true,
                ScoreSuggestion.Wrong => false,
                ScoreSuggestion.None => gebuchtKorrekt.Value,
                _ => Result.CorrectAnswered,
            };
        }
    }
}
