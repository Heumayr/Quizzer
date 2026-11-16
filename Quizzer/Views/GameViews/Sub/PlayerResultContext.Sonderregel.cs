using Quizzer.DataModels.Questions.Sonderregeln;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Sonderregel auf der Spielerkarte (M35 C3): richtig, falsch und ein Teilwert laufen durch die Regel
    /// der Zelle, der Vorschlag traegt ihren Grund (E1) und ist gekappt (E2). Ein eigener Wert von Hand
    /// bleibt, wie der Spielleiter ihn eintraegt.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Was dieser Spieler in der Vorphase abgegeben hat, etwa „Einsatz: 200"; leer ohne Regel.</summary>
        public string SonderregelText => Player == null ? string.Empty : CurrentQuestionViewModel?.SonderregelAbgabe(Player.Id) ?? string.Empty;

        public Visibility SonderregelVisibility => SonderregelText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Die Vorphase ist zu - die Karte zeigt die Abgabe.</summary>
        internal void SonderregelGeaendert()
        {
            OnPropertyChanged(nameof(SonderregelText));
            OnPropertyChanged(nameof(SonderregelVisibility));
        }

        /// <summary>Ein Teilwert der Typen mit Anteil - mit Sonderregel anteilig umgerechnet.</summary>
        private void SetzeTeilwert(int punkte)
        {
            CurrentScoreManipulation = punkte;
            WendeSonderregelAn(RatingKind.Partial, punkte);
        }

        /// <summary>Richtig und falsch durch die Regel - aus dem Setter von <see cref="Suggestion"/>.</summary>
        private void WendeSonderregelAufBewertungAn(ScoreSuggestion bewertung)
        {
            if (bewertung == ScoreSuggestion.Right)
                WendeSonderregelAn(RatingKind.Right, GetCurrentPoints());
            else if (bewertung == ScoreSuggestion.Wrong)
                WendeSonderregelAn(RatingKind.Wrong, -(Coordinate?.CurrentMinusPoints ?? 0));
        }

        private void WendeSonderregelAn(RatingKind bewertung, int grundwert)
        {
            if (Player == null || CurrentQuestionViewModel == null)
                return;

            var eingang = new SonderregelEingang(bewertung, grundwert, GetCurrentPoints(), new Kappung(Kontostand, UnterNullErlaubt));

            if (CurrentQuestionViewModel.SonderregelVorschlag(Player.Id, eingang) is not { } vorschlag)
                return;

            CurrentScoreManipulation = vorschlag.Punkte;
            Grund = vorschlag.Grund;
            OnPropertyChanged(nameof(Grund));
            OnPropertyChanged(nameof(SuggestionText));
        }
    }
}
