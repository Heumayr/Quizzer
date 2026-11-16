using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Bestenliste auf der Spielerkarte (M35 C9): Messwert der Runde und Klaus gehen beim Speichern ans Ergebnis.
    /// <para>
    /// <b>Der Rohwert ist Messung, kein Vorschlag</b> (E1 betrifft Punkte): eine andere Bewertung von Hand laesst ihn
    /// stehen. <b>Ein Klau zaehlt, wenn sein Vorschlag beim Speichern noch mit seinem Grund auf der Karte steht</b>;
    /// waehlt der Spielleiter eine andere Bewertung, ist er verworfen.
    /// </para>
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Ob die Runde schon Messwerte gemeldet hat - vorher schreibt das Speichern keinen Rohwert.</summary>
        private bool messwertGemeldet;

        /// <summary>Der zuletzt gemeldete Messwert; <c>null</c>: keiner (nichts abgegeben, ungueltig, Runde verworfen).</summary>
        public int? Messwert { get; private set; }

        /// <summary>Uebernimmt den Messwert der Runde - eine Wiederholung meldet neu und ersetzt ihn.</summary>
        internal void UebernimmMesswert(int? wert)
        {
            messwertGemeldet = true;
            Messwert = wert;
        }

        /// <summary>Schreibt Rohwert und Klaus ans Ergebnis - vor <see cref="SetManipulationToResult"/>, das den Vorschlag abraeumt.</summary>
        internal void SchreibeBestwerte(DateTimeOffset jetzt)
        {
            if (messwertGemeldet && FuehrtBestwert && Result.RecordValue != Messwert)
            {
                Result.RecordValue = Messwert;
                Result.RecordedAt = Messwert == null ? null : jetzt;
            }

            Result.Steals += GespeicherteKlaus();
        }

        /// <summary>Nur Arten mit Bestwert - der Server weist einen Rohwert an jeder anderen Frage ab.</summary>
        private bool FuehrtBestwert
            => Coordinate?.QuestionBase is MinigameQuestion frage
               && MinigameProfiles.TryFor(frage.Kind, out var profil)
               && profil.Record != null;

        /// <summary>Die Klaus des Vorschlags, der noch mit seinem Grund auf der Karte steht - Minispiel oder Sonderregel.</summary>
        private int GespeicherteKlaus()
        {
            if (Suggestion == ScoreSuggestion.None || Grund == null)
                return 0;

            foreach (var vorschlag in new[] { letzterVorschlag, regelVorschlag })
            {
                if (vorschlag is { Klaus: > 0 } && vorschlag.Grund == Grund)
                    return vorschlag.Klaus;
            }

            return 0;
        }
    }
}
