using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Der Hinweis über dem Raster des Beamers: wer die nächste Frage wählt (M30). Er steht auch
    /// bei ausgeblendetem Punktestand da - die Plakette auf der Kachel sähe man dann nicht.
    /// Während einer Frage und nach dem Spiel blendet die Ansicht ihn aus (Trigger in der XAML).
    /// </summary>
    public partial class GamePlayerViewModel
    {
        public string WaehlerName => GameMasterViewModel?.ChoosingPlayerItem?.CalculatedDisplayName ?? string.Empty;

        public string WaehlerHinweis => $"{WaehlerName} wählt die nächste Frage";

        public Visibility WaehlerHinweisVisibility => string.IsNullOrEmpty(WaehlerName) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Vom Spielleiter nach jedem Wechsel des Wählers gerufen.</summary>
        internal void WaehlerGeaendert()
        {
            OnPropertyChanged(nameof(WaehlerName));
            OnPropertyChanged(nameof(WaehlerHinweis));
            OnPropertyChanged(nameof(WaehlerHinweisVisibility));
        }
    }
}
