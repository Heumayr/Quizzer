using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Views.GameViews.Minispiele;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Erklaerbaer im Fragefenster (M35 C6): der Begriff steht nur hier, beim Spielleiter - er muss hoeren koennen,
    /// ob der Erklaerer ihn sagt - und „Verraten" wertet die Runde. Die Paarwahl zeigt nur den Erklaerer.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private AsyncRelayCommand? verratenCommand;

        public ICommand VerratenCommand
            => verratenCommand ??= new AsyncRelayCommand(_ => (ablauf as ErklaerbaerAblauf)?.VerratenAsync() ?? Task.CompletedTask);

        public Visibility ErklaerbaerVisibility
            => MinispielSteuerungVisibility == Visibility.Visible && Minispiel?.Kind == MinigameKind.ExplainIt
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Nur im Spielleiterfenster, nie am Beamer.</summary>
        public string ErklaerbaerBegriff
            => (ablauf as ErklaerbaerAblauf)?.Begriff is { } begriff
                ? $"Begriff (nur für dich): {begriff}"
                : "Der Begriff erscheint hier mit dem Start der Runde.";

        /// <summary>Ein Gegenueber waehlt der Spielleiter nur, wo die Art eines hat - beim Erklaerbaer nicht.</summary>
        public Visibility PaarPartnerVisibility => SpielerwahlArt?.ChooserOnly == true ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Beschriftungen der Paarwahl und der Erklaerbaer-Teil - aus <c>BereitePaarVor</c> und aus dem Ablauf.</summary>
        internal void PaarwahlGeaendert()
        {
            OnPropertyChanged(nameof(PaarPartnerBeschriftung));
            OnPropertyChanged(nameof(PaarWaehlerBeschriftung));
            OnPropertyChanged(nameof(ErklaerbaerBegriff));
            OnPropertyChanged(nameof(ErklaerbaerVisibility));
            OnPropertyChanged(nameof(PaarPartnerVisibility));
            OnPropertyChanged(nameof(PaarKnopfBeschriftung));
        }
    }
}
