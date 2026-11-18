using Quizzer.DataModels.Enumerations;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// „Runde zurücksetzen" nach dem Ausspielen der Antworten (V22; seit P2-3 jede Eingabe- und Tastenfrage, nicht nur Teamregel
    /// und Finale): der Hub verwirft die Abgaben (ResetLayouts, Reopen der Team-Abstimmung) - hier gehen Antworten, Tasten,
    /// Schätzauswertung, Vergleich bzw. Abstimmung, Auflösung am Beamer und die Vorschläge der Karten mit. Danach steht im
    /// Fragefenster wieder, was vor der ersten Abgabe stand; nichts Altes ist mehr sichtbar oder wertbar.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Nach dem Ausspielen der Antworten (Eingabe oder Tastenwahl), mit und ohne Teamregel (P2-3).</summary>
        private void MeldeAntwortRundeAn(BuzzerControlsLayout layout)
        {
            if (BuzzerControlsViewModel == null || layout is not (BuzzerControlsLayout.Input or BuzzerControlsLayout.KeySelect))
                return;

            BuzzerControlsViewModel.AnswerRoundReset = OnAntwortRundeReset;
        }

        private void OnAntwortRundeReset()
        {
            // Erst die Regel, dann die Karten - jede Karte wertet beim Zurücksetzen die Zelle neu, und das soll ohne Altes geschehen.
            GleichGedachtRegel?.VerwirfAntworten();
            TeamAbstimmung?.VerwirfStimmen();
            LeereFinale();
            LeereGleichGedacht();
            LeereTeamAbstimmung();
            ClearAppreciateOutcome();
            Sonderregel.ZeigeAufloesung([]);

            LeereKarten();

            if (Finale != null)
                ZeigeFinale();
            else if (Teamregel is { } regel)
                ZeigeTeamregel(regel);

            WerteSonderregel();
            BeratungszeitNachZuruecksetzen();
        }

        /// <summary>Antworten und Bewertungen der alten Runde - auch eine von Hand: sie galt einer Antwort, die es nicht mehr gibt.</summary>
        private void LeereKarten()
        {
            if (PlayersResultViewModel is not { } ergebnis)
                return;

            ergebnis.SelectionResultsDic = null;

            foreach (var karte in ergebnis.PlayerResultContextList)
            {
                karte.InputResult = null;
                karte.AppreciateGuess = null;
                karte.VergissOhneWertungVonHand();

                if (karte.Suggestion != ScoreSuggestion.None || karte.CurrentScoreManipulation != 0)
                    karte.Suggestion = ScoreSuggestion.None;

                karte.SonderregelGeaendert();
            }
        }
    }
}
