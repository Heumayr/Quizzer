using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Minispiel-Teil des Ergebnisfensters (M35 B2): wertet die Mehrheit ueber alle Karten aus,
    /// sobald eine Abgabe hereinkommt - eine einzelne Karte kann nicht wissen, ob ihre Gruppe vorn liegt.
    /// Die Reaktion (B3) wertet nach jeder geschlossenen Runde ueber alle bisherigen.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>Das letzte Ergebnis der Mehrheit - <c>null</c>, solange keine Minispiel-Zelle laeuft.</summary>
        public MehrheitErgebnis? MehrheitErgebnis { get; private set; }

        /// <summary>Das letzte Ergebnis der Reaktion - <c>null</c>, solange keine Runde gezaehlt ist.</summary>
        public ReaktionErgebnis? ReaktionErgebnis { get; private set; }

        /// <summary>
        /// Rechnet die Mehrheit neu und verteilt die Vorschlaege. Gerufen von jeder Karte, deren Abgabe
        /// sich aendert; zaehlt nur Tasten, die zu einer Option gehoeren.
        /// </summary>
        internal void WerteMinispiel()
        {
            // Nur die Mehrheit waehlt Tasten - eine andere Art darf hier nicht ihre Karten leeren.
            if (Coordinate?.QuestionBase is not MinigameQuestion { Kind: MinigameKind.Majority } frage)
                return;

            var abgaben = PlayerResultContextList.ToDictionary(
                c => c.Player.Id,
                c => c.SelectionResult?.SelectedKeys.FirstOrDefault());

            var tasten = frage.Steps
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly && !string.IsNullOrEmpty(s.QuestionViewKey))
                .Select(s => s.QuestionViewKey);

            var minderheit = MehrheitEvaluator.IsMinority(frage);

            MehrheitErgebnis = MehrheitEvaluator.Werte(abgaben, tasten, minderheit);

            foreach (var ctx in PlayerResultContextList)
                ctx.UebernimmMehrheit(MehrheitErgebnis, frage, minderheit);

            OnPropertyChanged(nameof(MehrheitErgebnis));
        }

        /// <summary>Tippsprint (M35 C1): meiste Tipps „richtig", sonst keiner, nie Minus (E1).</summary>
        internal void WerteTippsprint(TippsprintErgebnis ergebnis)
        {
            foreach (var ctx in PlayerResultContextList)
                ctx.UebernimmTippsprint(ergebnis);
        }

        /// <summary>Stoppuhr (M35 C1): naechste an der Zielzeit „richtig", sonst keiner, nie Minus (E1).</summary>
        internal void WerteStoppuhr(StoppuhrErgebnis ergebnis)
        {
            foreach (var ctx in PlayerResultContextList)
                ctx.UebernimmStoppuhr(ergebnis);
        }

        /// <summary>Verteilt die Vorschlaege der Reaktion: beste Zeit „richtig", sonst keiner, nie Minus (E1).</summary>
        internal void WerteReaktion(ReaktionErgebnis ergebnis)
        {
            ReaktionErgebnis = ergebnis;

            foreach (var ctx in PlayerResultContextList)
                ctx.UebernimmReaktion(ergebnis);

            OnPropertyChanged(nameof(ReaktionErgebnis));
        }
    }
}
