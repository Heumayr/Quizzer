using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Stoppuhr im Spielablauf (M35 C1): die geschlossene Runde wird mit der Abstandswertung der
    /// Schaetzfrage gewertet - kleinster Abstand „richtig", sonst nichts, nie Minus.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Die Versuche der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private Dictionary<Guid, StoppuhrVersuch> stoppuhrAbgaben = new();

        private void StoppuhrRundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                stoppuhrAbgaben = runde.Submissions
                    .Where(a => a.Value is StoppuhrVersuch)
                    .ToDictionary(a => a.Key, a => (StoppuhrVersuch)a.Value);
            }

            var ergebnis = WerteStoppuhr();

            Reaktion.Zeige(runde.Aborted
                    ? "Minispiel abgebrochen – der Stand"
                    : $"Ergebnis – Ziel {Minispiel?.DurationSeconds ?? 0} Sekunden, die nächste Zeit gewinnt",
                StoppuhrTafel.Ende(ergebnis, MinispielSpieler()));
        }

        private StoppuhrErgebnis WerteStoppuhr()
        {
            var ergebnis = StoppuhrEvaluator.Werte(stoppuhrAbgaben, Minispiel?.DurationSeconds ?? 0);
            PlayersResultViewModel?.WerteStoppuhr(ergebnis);
            return ergebnis;
        }

        /// <summary>Vergisst die Versuche; <c>true</c>, wenn schon gewertet war.</summary>
        private bool LeereStoppuhr()
        {
            var gewertet = stoppuhrAbgaben.Count > 0;
            stoppuhrAbgaben = new();
            return gewertet;
        }
    }
}
