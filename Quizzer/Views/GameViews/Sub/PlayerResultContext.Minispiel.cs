using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Minispiel-Teil der Spielerkarte (M35 B2): die Wahl des Spielers, seine Gruppe und der
    /// Vorschlag aus der Mehrheit. Gerechnet wird ueber alle Karten in
    /// <c>PlayersResultViewModel.WerteMinispiel</c>, hier nur uebernommen.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>
        /// Was die Mehrheit fuer diese Karte zuletzt ergab - <c>null</c>: kein Vorschlag. Nur ein
        /// <b>Wechsel</b> setzt den Vorschlag neu; was der Spielleiter von Hand setzt, bleibt stehen,
        /// solange eine spaetere Abgabe die Gruppe nicht aendert.
        /// </summary>
        private bool? mehrheitGewinner;

        /// <summary>Uebernimmt das Ergebnis der Mehrheit fuer diesen Spieler.</summary>
        internal void UebernimmMehrheit(MehrheitErgebnis ergebnis, QuestionBase frage, bool minderheit)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(frage);

            istZuordnung = false;
            Reihenfolge = null;

            if (!ergebnis.Abgaben.TryGetValue(Player.Id, out var taste))
            {
                // Wer nicht (gueltig) gewaehlt hat, bekommt keinen Vorschlag.
                ZeigeMehrheit([], Visibility.Collapsed, null);
                WechsleVorschlag(null);
                return;
            }

            var gewinner = ergebnis.IstGewinner(Player.Id);
            var schritt = frage.Steps.FirstOrDefault(s => s.QuestionViewKey == taste);
            var gruppe = minderheit ? "kleinsten Gruppe" : "größten Gruppe";

            // Nie allein ueber Farbe: der Satz sagt, ob die Wahl gewinnt.
            var satz = $"{ergebnis.GruppeVon(Player.Id)} von {ergebnis.Abgaben.Count} Stimmen - "
                       + (gewinner ? $"in der {gruppe}" : $"nicht in der {gruppe}");

            ZeigeMehrheit(
                [new KeyView
                {
                    Key = taste,
                    Designation = schritt?.Designation ?? schritt?.StepText,
                    IsCorrect = gewinner,
                    ShowDesignation = true,
                }],
                Visibility.Visible,
                satz);

            WechsleVorschlag(gewinner);
        }

        private void ZeigeMehrheit(List<KeyView> tasten, Visibility sichtbar, string? satz)
        {
            KeyViews = tasten;
            ShowKeySelections = sichtbar;
            ReihenfolgeTextUeberschrieben = satz;

            OnPropertyChanged(nameof(Reihenfolge));
            OnPropertyChanged(nameof(ReihenfolgeText));
            OnPropertyChanged(nameof(ReihenfolgeVisibility));
            OnPropertyChanged(nameof(KeyViews));
            OnPropertyChanged(nameof(ShowKeySelections));
        }

        /// <summary>E1: Gewinner „richtig" mit den Feldpunkten, alle anderen offen - nie „falsch".</summary>
        private void WechsleVorschlag(bool? gewinner)
        {
            if (gewinner == mehrheitGewinner)
                return;

            if (gewinner == true)
                Suggestion = ScoreSuggestion.Right;
            else if (mehrheitGewinner == true)
                Suggestion = ScoreSuggestion.None;

            mehrheitGewinner = gewinner;
        }
    }
}
