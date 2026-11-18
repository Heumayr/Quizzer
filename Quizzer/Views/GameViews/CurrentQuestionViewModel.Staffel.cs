using Quizzer.Base;
using Quizzer.Views.GameViews.Minispiele;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Staffel im Fragefenster (M37 T11): die Spielleitung hat das letzte Wort (E1) - sie lässt die zuletzt abgelehnte Antwort gelten
    /// (auch nach dem Ende, wenn sie die letzte war) oder gibt für das Team am Zug weiter, etwa wenn das Telefon des Läufers fehlt.
    /// Beides geschieht am Server; jedes Telefon erfährt es.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private AsyncRelayCommand? relayAcceptCommand;
        private AsyncRelayCommand? relayPassCommand;

        public ICommand RelayAcceptCommand
            => relayAcceptCommand ??= new AsyncRelayCommand(_ => LasseStaffelAntwortGeltenAsync());

        public ICommand RelayPassCommand
            => relayPassCommand ??= new AsyncRelayCommand(_ => GibStaffelWeiterAsync());

        /// <summary>Nur, solange eine abgelehnte Antwort der letzte Versuch ist.</summary>
        public Visibility StaffelGeltenVisibility
            => MinispielSteuerungVisibility == Visibility.Visible && (ablauf as StaffelAblauf)?.KannGelten == true
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Nur, solange die Staffel läuft.</summary>
        public Visibility StaffelWeitergebenVisibility
            => MinispielSteuerungVisibility == Visibility.Visible && (ablauf as StaffelAblauf)?.KannWeitergeben == true
                ? Visibility.Visible : Visibility.Collapsed;

        internal async Task LasseStaffelAntwortGeltenAsync()
        {
            if (BuzzerControlsViewModel == null || ablauf is not StaffelAblauf staffel)
                return;

            if (!await BuzzerControlsViewModel.AcceptLastRelayAnswerAsync())
            {
                UserPrompt.Inform("Gelten lassen geht nur für die zuletzt abgelehnte Antwort, solange danach niemand geantwortet hat.", MinispielCaption);
                return;
            }

            Klang.Tick();
            staffel.NachEingriff();

            // V26: nach der Buchung steht der Unterschied als Vorschlag auf den Karten - das Bewertungsfenster zeigt ihn.
            if (PlayersResultViewModel?.HatGebucht == true)
                await OpenResultsAsync();
        }

        internal async Task GibStaffelWeiterAsync()
        {
            if (BuzzerControlsViewModel == null || ablauf is not StaffelAblauf staffel)
                return;

            if (!await BuzzerControlsViewModel.PassRelayAsync())
            {
                UserPrompt.Inform("Weitergeben geht nur, solange die Staffel läuft.", MinispielCaption);
                return;
            }

            staffel.NachEingriff();
        }

        /// <summary>Aus dem Ablauf: der Stand hat sich geändert - die Knöpfe der Spielleitung kommen oder gehen.</summary>
        internal void StaffelGeaendert()
        {
            OnPropertyChanged(nameof(StaffelGeltenVisibility));
            OnPropertyChanged(nameof(StaffelWeitergebenVisibility));
        }
    }
}
