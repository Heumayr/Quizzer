using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Raeuber und Waechter im Fragefenster (M35 C2): eine Runde, dann die Aufloesung am Beamer und je
    /// Karte die Summe ihrer Bewegungen mit Grund - beim Dieb und beim Opfer, einmal gekappt (E2).
    /// </summary>
    internal sealed class RaeuberAblauf : MinispielAblauf
    {
        /// <summary>Die Wahlen der gezaehlten Runde; ein Abbruch danach behaelt sie.</summary>
        private Dictionary<Guid, RaeuberWahl> wahlen = new();

        public RaeuberAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.RobberAndGuard)
        {
        }

        public override void ZeigeLaeuft()
            => Anzeige.Zeige("Räuber und Wächter – wählt geheim: bestehlen oder Schild!");

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                wahlen = runde.Submissions
                    .Where(a => a.Value is RaeuberWahl)
                    .ToDictionary(a => a.Key, a => (RaeuberWahl)a.Value);
            }

            var ergebnis = Werte();

            Anzeige.Zeige(runde.Aborted ? "Minispiel abgebrochen – der Stand" : "Räuber und Wächter – die Auflösung",
                SpielerwahlTafel.Raeuber(ergebnis, Spieler()));
        }

        /// <summary>Eine Runde gibt es nur einmal - Wiederholen vergisst sie wie das Zuruecksetzen.</summary>
        public override void Wiederhole(int runde) => SetzeZurueck();

        public override void SetzeZurueck()
        {
            var gewertet = wahlen.Count > 0;
            wahlen = new();

            if (gewertet)
                Werte();
        }

        private RaeuberErgebnis Werte()
        {
            var ergebnis = RaeuberEvaluator.Werte(wahlen, Punkteanteil.Von(Feldpunkte, Frage?.Share ?? 0), Konten(), Name);

            Karten?.WerteVorschlaege(ergebnis.Vorschlaege, id => RaeuberEvaluator.Describe(ergebnis, id, Name));
            return ergebnis;
        }
    }
}
