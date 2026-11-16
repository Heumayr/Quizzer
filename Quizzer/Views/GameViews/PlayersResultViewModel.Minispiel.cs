using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Minispiel-Teil des Ergebnisfensters (M35 B2): wertet die Mehrheit ueber alle Karten aus,
    /// sobald eine Abgabe hereinkommt - eine einzelne Karte kann nicht wissen, ob ihre Gruppe vorn liegt.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>Das letzte Ergebnis der Mehrheit - <c>null</c>, solange keine Minispiel-Zelle laeuft.</summary>
        public MehrheitErgebnis? MehrheitErgebnis { get; private set; }

        /// <summary>
        /// Rechnet die Mehrheit neu und verteilt die Vorschlaege. Gerufen von jeder Karte, deren Abgabe
        /// sich aendert; zaehlt nur Tasten, die zu einer Option gehoeren.
        /// </summary>
        internal void WerteMinispiel()
        {
            if (Coordinate?.QuestionBase is not MinigameQuestion frage)
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
    }
}
