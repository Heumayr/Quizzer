using Quizzer.Base;
using Quizzer.Views.GameViews.Minispiele;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Stellvertreter-Duell im Fragefenster (M37 T10): wählt ein Team nicht, lost der Spielleiter für alle Teams ohne
    /// Stellvertreter (E1) - gelost wird am Server, was schon gewählt ist, bleibt.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private AsyncRelayCommand? drawProxiesCommand;

        public ICommand DrawProxiesCommand
            => drawProxiesCommand ??= new AsyncRelayCommand(_ => LoseStellvertreterAsync());

        /// <summary>Nur, solange beim Stellvertreter-Duell noch ein Team wählt.</summary>
        public Visibility StellvertreterLosenVisibility
            => MinispielSteuerungVisibility == Visibility.Visible && (ablauf as StellvertreterAblauf)?.WahlOffen == true
                ? Visibility.Visible : Visibility.Collapsed;

        internal async Task LoseStellvertreterAsync()
        {
            if (BuzzerControlsViewModel == null || ablauf is not StellvertreterAblauf duell)
                return;

            if (!await BuzzerControlsViewModel.DrawOpenProxiesAsync())
            {
                UserPrompt.Inform("Gerade wählt kein Team – ausgelost wird nur, solange die Wahl läuft.", MinispielCaption);
                return;
            }

            Klang.Tick();
            duell.ZeigeLaeuft();
        }

        /// <summary>Aus dem Ablauf: die Wahl hat sich geändert - der Knopf zum Auslosen kommt oder geht.</summary>
        internal void StellvertreterGeaendert() => OnPropertyChanged(nameof(StellvertreterLosenVisibility));
    }
}
