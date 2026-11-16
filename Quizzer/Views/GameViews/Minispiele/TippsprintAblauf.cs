using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Tippsprint im Fragefenster (M35 C1): die Zwischenstaende treiben das Rennen am Beamer, die
    /// Endstaende der geschlossenen Runde die Wertung - meiste Tipps „richtig", sonst nichts, nie Minus.
    /// </summary>
    internal sealed class TippsprintAblauf : MinispielAblauf
    {
        /// <summary>Der letzte angenommene Stand je Spieler - das Rennen.</summary>
        private readonly Dictionary<Guid, TippsprintStand> staende = new();

        /// <summary>Die Endstaende der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private Dictionary<Guid, int> endstaende = new();

        public TippsprintAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.TapSprint)
        {
        }

        public override void ZeigeLaeuft()
            => Anzeige.Zeige($"Tippsprint läuft – {Frage?.DurationSeconds ?? 0} Sekunden, tippt!",
                TippsprintTafel.Rennen(staende, Spieler()));

        /// <summary>Ein Stand ist angenommen: das Rennen laeuft mit, solange die Runde laeuft.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            // Nach dem Schliessen steht das Ergebnis am Beamer - ein spaet verteilter Stand darf es nicht ueberschreiben.
            if (abgabe.Value is not TippsprintStand stand || Zustand?.Phase != MinigamePhase.Running)
                return;

            staende[abgabe.Player.Id] = stand;
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                endstaende = runde.Submissions
                    .Where(a => a.Value is TippsprintStand { Ende: true })
                    .ToDictionary(a => a.Key, a => ((TippsprintStand)a.Value).Tipps);
            }

            var ergebnis = Werte();

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : "Ergebnis – die meisten Tipps gewinnen",
                TippsprintTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst Rennen und Endstaende; war schon gewertet, auch die Vorschlaege.</summary>
        public override void SetzeZurueck()
        {
            var gewertet = endstaende.Count > 0;

            staende.Clear();
            endstaende = new();

            if (gewertet)
                Werte();
        }

        private TippsprintErgebnis Werte()
        {
            var ergebnis = TippsprintEvaluator.Werte(endstaende);
            Karten?.WerteTippsprint(ergebnis);
            return ergebnis;
        }
    }
}
