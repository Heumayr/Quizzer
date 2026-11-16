using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Stoppuhr im Fragefenster (M35 C1): die geschlossene Runde wird mit der Abstandswertung der
    /// Schaetzfrage gewertet - kleinster Abstand „richtig", sonst nichts, nie Minus.
    /// </summary>
    internal sealed class StoppuhrAblauf : MinispielAblauf
    {
        /// <summary>Die Versuche der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private Dictionary<Guid, StoppuhrVersuch> abgaben = new();

        public StoppuhrAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.Stopwatch)
        {
        }

        public override void ZeigeLaeuft()
            => Anzeige.Zeige($"Stoppuhr läuft – stoppt nach genau {Frage?.DurationSeconds} Sekunden!");

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                abgaben = runde.Submissions
                    .Where(a => a.Value is StoppuhrVersuch)
                    .ToDictionary(a => a.Key, a => (StoppuhrVersuch)a.Value);
            }

            var ergebnis = Werte();

            Anzeige.Zeige(runde.Aborted
                    ? "Minispiel abgebrochen – der Stand"
                    : $"Ergebnis – Ziel {Frage?.DurationSeconds ?? 0} Sekunden, die nächste Zeit gewinnt",
                StoppuhrTafel.Ende(ergebnis, Spieler()));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        /// <summary>Vergisst die Versuche; war schon gewertet, auch die Vorschlaege.</summary>
        public override void SetzeZurueck()
        {
            var gewertet = abgaben.Count > 0;
            abgaben = new();

            if (gewertet)
                Werte();
        }

        private StoppuhrErgebnis Werte()
        {
            var ergebnis = StoppuhrEvaluator.Werte(abgaben, Frage?.DurationSeconds ?? 0);
            Karten?.WerteStoppuhr(ergebnis);
            return ergebnis;
        }
    }
}
