using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Reihenfolge-Teil der Spielerkarte (M12): zeigt die Abgabe Platz für Platz und schlägt die
    /// Punkte selbst vor - alles richtig gibt „richtig", ein Teil richtig einen eigenen Wert.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Die Auswertung der Abgabe - <c>null</c>, solange keine vorliegt.</summary>
        public ReihenfolgeErgebnis? Reihenfolge { get; private set; }

        /// <summary>Was unter den Tasten steht, etwa „2 von 4 Plätzen richtig".</summary>
        public string ReihenfolgeText => Reihenfolge == null
            ? string.Empty
            : $"{Reihenfolge.RichtigePlaetze} von {Reihenfolge.Gesamt} Plätzen richtig";

        public Visibility ReihenfolgeVisibility
            => Reihenfolge == null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Wertet die Abgabe aus und setzt den Vorschlag.
        /// <para>
        /// <b>Teilweise richtig wird zum eigenen Wert</b> (<c>Correction</c>), nicht zu „richtig":
        /// „richtig" zählt in der Statistik als richtig beantwortet und gibt immer den vollen
        /// Wert. Der eigene Wert bleibt im Feld änderbar - der Spielleiter kann abrunden.
        /// </para>
        /// <para>
        /// <b>Keine Minuspunkte von selbst</b>, wie bei der Schätzfrage: eine ganz falsche
        /// Reihenfolge bleibt auf „nicht bewertet".
        /// </para>
        /// </summary>
        /// <returns><c>false</c>, wenn die Frage keine eindeutige Lösung trägt - dann zeigt die
        /// Karte die Tasten wie bei Multiple Choice und der Spielleiter bewertet von Hand.</returns>
        private bool WerteReihenfolge(QuestionBase question, List<string> selectedKeys)
        {
            var ergebnis = ReihenfolgeEvaluator.Werte(question, selectedKeys, GetCurrentPoints());

            Reihenfolge = ergebnis;
            OnPropertyChanged(nameof(Reihenfolge));
            OnPropertyChanged(nameof(ReihenfolgeText));
            OnPropertyChanged(nameof(ReihenfolgeVisibility));

            if (ergebnis == null)
                return false;

            KeyViews = ergebnis.Plaetze
                .Select(p => new KeyView
                {
                    Key = $"{p.Nummer}. {(p.Taste.Length == 0 ? "–" : p.Taste)}",
                    Designation = p.Text,
                    IsCorrect = p.Richtig,
                    ShowDesignation = true,
                })
                .ToList();

            if (ergebnis.AllesRichtig)
            {
                Suggestion = ScoreSuggestion.Right;
            }
            else if (ergebnis.RichtigePlaetze > 0)
            {
                Suggestion = ScoreSuggestion.Correction;
                CurrentScoreManipulation = ergebnis.Punkte;
            }
            else
            {
                Suggestion = ScoreSuggestion.None;
            }

            ShowKeySelections = Visibility.Visible;
            OnPropertyChanged(nameof(KeyViews));
            OnPropertyChanged(nameof(ShowKeySelections));

            return true;
        }
    }
}
