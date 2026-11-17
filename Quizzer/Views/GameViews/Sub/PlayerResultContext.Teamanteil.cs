using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Minispiele je Team auf der Spielerkarte (M37 T8): bei einer zusammengefassten Art steht das eigene Ergebnis nur im Satz,
    /// der Vorschlag ist der Anteil am Teamwert. Gerechnet wird über alle Karten im <c>PlayersResultViewModel</c>.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Ob der Vorschlag dieser Karte vom Team kommt - ein Teamspiel und eine zusammengefasste Art.</summary>
        internal bool WertetMinispielAlsTeam => Teamwertung.FasstMinispielZusammen(Coordinate?.Game, Coordinate?.QuestionBase);

        /// <summary>Was der Teamanteil dieser Karte zuletzt war.</summary>
        private Punktevorschlag? letzterTeamanteil;

        /// <summary>Übernimmt den Anteil am Teamwert - nur ein Wechsel setzt neu; was der Spielleiter danach setzt, bleibt (E1).</summary>
        internal void UebernimmTeamanteil(Punktevorschlag? anteil)
        {
            if (anteil == letzterTeamanteil)
                return;

            if (anteil != null)
                SchlageVor(anteil.Punkte, anteil.Grund);
            else if (letzterTeamanteil != null)
                Suggestion = ScoreSuggestion.None;

            letzterTeamanteil = anteil;
        }
    }
}
