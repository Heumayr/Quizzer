using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>Reaktion im Fragefenster (M35 B3): beste Einzelzeit ueber alle gezaehlten Runden.</summary>
    internal sealed class ReaktionAblauf : MinispielAblauf
    {
        /// <summary>Die gezaehlten Runden; eine Wiederholung ersetzt ihre Runde.</summary>
        private readonly SortedDictionary<int, IReadOnlyDictionary<Guid, ReactionAttempt>> runden = new();

        public ReaktionAblauf(CurrentQuestionViewModel fenster, MinigameKind? art)
            : base(fenster, art)
        {
        }

        public override void ZeigeLaeuft()
        {
            var zustand = Zustand;
            Anzeige.Zeige($"Runde {zustand?.Round ?? 0} von {zustand?.Setup.TotalRounds ?? 0} – wartet auf Grün!");
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
            {
                runden[runde.Round] = runde.Submissions
                    .Where(a => a.Value is ReactionAttempt)
                    .ToDictionary(a => a.Key, a => (ReactionAttempt)a.Value);
            }

            var ergebnis = Werte();
            var spieler = Spieler();

            if (runde.Aborted)
                Anzeige.Zeige("Minispiel abgebrochen – die besten Zeiten", ReaktionTafel.Ende(ergebnis, spieler));
            else if (runde.Finished)
                Anzeige.Zeige("Ergebnis – die beste Zeit gewinnt", ReaktionTafel.Ende(ergebnis, spieler));
            else
                Anzeige.Zeige($"Runde {runde.Round} von {runde.TotalRounds} – die Zeiten",
                    ReaktionTafel.Runde(runden[runde.Round], spieler));
        }

        public override void Wiederhole(int runde)
        {
            runden.Remove(runde);
            Werte();
        }

        public override void SetzeZurueck()
        {
            if (runden.Count == 0)
                return;

            runden.Clear();
            Werte();
        }

        private ReaktionErgebnis Werte()
        {
            var ergebnis = ReaktionEvaluator.Werte(runden.Values);
            Karten?.WerteReaktion(ergebnis);
            return ergebnis;
        }
    }
}
