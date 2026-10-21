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
            ? ReihenfolgeTextUeberschrieben ?? string.Empty
            : istZuordnung
                ? $"{Reihenfolge.RichtigePlaetze} von {Reihenfolge.Gesamt} Paaren richtig"
                : $"{Reihenfolge.RichtigePlaetze} von {Reihenfolge.Gesamt} Plätzen richtig";

        private bool istZuordnung;

        public Visibility ReihenfolgeVisibility
            => string.IsNullOrEmpty(ReihenfolgeText) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Wahr oder falsch (M12): je Aussage, ob der Spieler richtig lag; der Vorschlag wie bei
        /// der Reihenfolge - alles richtig „richtig", ein Teil ein eigener Wert, nichts „offen".
        /// </summary>
        private void WerteWahrFalsch(QuestionBase question, List<string> wahrGetippt)
        {
            var ergebnis = WahrFalschEvaluator.Werte(question, wahrGetippt, GetCurrentPoints());

            istZuordnung = false;
            Reihenfolge = null;
            OnPropertyChanged(nameof(Reihenfolge));

            ReihenfolgeTextUeberschrieben = $"{ergebnis.Richtige} von {ergebnis.Gesamt} Aussagen richtig beurteilt";
            OnPropertyChanged(nameof(ReihenfolgeText));
            OnPropertyChanged(nameof(ReihenfolgeVisibility));

            KeyViews = ergebnis.Urteile
                .Select(u => new KeyView
                {
                    Key = $"{u.Taste}: {(u.GetipptWahr ? "wahr" : "falsch")}",
                    Designation = u.Text,
                    IsCorrect = u.Richtig,
                    ShowDesignation = true,
                })
                .ToList();

            if (ergebnis.AllesRichtig)
            {
                Suggestion = ScoreSuggestion.Right;
            }
            else if (ergebnis.Richtige > 0)
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
        }

        /// <summary>Der Satz unter den Tasten bei „Wahr oder falsch".</summary>
        private string? ReihenfolgeTextUeberschrieben;

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

            istZuordnung = question.Typ == DataModels.Enumerations.QuestionType.Matching;
            ReihenfolgeTextUeberschrieben = null;
            Reihenfolge = ergebnis;
            OnPropertyChanged(nameof(Reihenfolge));
            OnPropertyChanged(nameof(ReihenfolgeText));
            OnPropertyChanged(nameof(ReihenfolgeVisibility));

            if (ergebnis == null)
                return false;

            // Bei der Zuordnung steht der Begriff mit dabei - "1 → B" allein sagt dem Spielleiter
            // nicht, welches Paar falsch ist.
            var begriffe = ReihenfolgeEvaluator.Loesung(question)!.Select(s => s.PartnerText).ToList();

            KeyViews = ergebnis.Plaetze
                .Select(p => new KeyView
                {
                    Key = istZuordnung
                        ? $"{p.Nummer} {begriffe[p.Nummer - 1]} → {(p.Taste.Length == 0 ? "–" : p.Taste)}"
                        : $"{p.Nummer}. {(p.Taste.Length == 0 ? "–" : p.Taste)}",
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
