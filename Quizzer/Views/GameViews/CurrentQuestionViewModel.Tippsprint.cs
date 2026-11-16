using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Tippsprint im Spielablauf (M35 C1): die Zwischenstaende treiben das Rennen am Beamer, die
    /// Endstaende der geschlossenen Runde die Wertung - meiste Tipps „richtig", sonst nichts, nie Minus.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Der letzte angenommene Stand je Spieler - das Rennen.</summary>
        private readonly Dictionary<Guid, TippsprintStand> sprintStaende = new();

        /// <summary>Die Endstaende der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private Dictionary<Guid, int> sprintEndstaende = new();

        /// <summary>Ein Stand ist angenommen: das Rennen laeuft mit, solange die Runde laeuft.</summary>
        internal Task OnMinigameSubmittedAsync(MinigameSubmission abgabe)
        {
            // Nach dem Schliessen steht das Ergebnis am Beamer - ein spaet verteilter Stand darf es nicht ueberschreiben.
            if (Minispiel?.Kind != MinigameKind.TapSprint || abgabe.Value is not TippsprintStand stand
                || BuzzerControlsViewModel?.MinigameState?.Phase != MinigamePhase.Running)
                return Task.CompletedTask;

            sprintStaende[abgabe.Player.Id] = stand;
            ZeigeRennen();
            return Task.CompletedTask;
        }

        private void ZeigeRennen()
            => Reaktion.Zeige($"Tippsprint läuft – {Minispiel?.DurationSeconds ?? 0} Sekunden, tippt!",
                TippsprintTafel.Rennen(sprintStaende, MinispielSpieler()));

        private void TippsprintRundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                sprintEndstaende = runde.Submissions
                    .Where(a => a.Value is TippsprintStand { Ende: true })
                    .ToDictionary(a => a.Key, a => ((TippsprintStand)a.Value).Tipps);
            }

            var ergebnis = WerteTippsprint();

            Reaktion.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : "Ergebnis – die meisten Tipps gewinnen",
                TippsprintTafel.Ende(ergebnis, MinispielSpieler()));
        }

        private TippsprintErgebnis WerteTippsprint()
        {
            var ergebnis = TippsprintEvaluator.Werte(sprintEndstaende);
            PlayersResultViewModel?.WerteTippsprint(ergebnis);
            return ergebnis;
        }

        /// <summary>Vergisst Rennen und Endstaende; <c>true</c>, wenn schon gewertet war.</summary>
        private bool LeereTippsprint()
        {
            var gewertet = sprintEndstaende.Count > 0;

            sprintStaende.Clear();
            sprintEndstaende = new();

            return gewertet;
        }
    }
}
