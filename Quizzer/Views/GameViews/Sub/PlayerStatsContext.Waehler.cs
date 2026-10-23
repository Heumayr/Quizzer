using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>Plakette „wählt" auf der Punktekachel (M30).</summary>
    public partial class PlayerStatsContext
    {
        /// <summary>Ob dieser Spieler als Nächstes die Frage wählt. Nach dem Spiel wählt niemand mehr.</summary>
        public bool IsChoosing => Player != null && !IsGameFinished && Game?.CurrentChoosingPlayerId == Player.Id;

        public Visibility ChoosingVisibility => IsChoosing ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Vom Spielleiter nach jedem Wechsel des Wählers gerufen.</summary>
        internal void WaehlerGeaendert()
        {
            OnPropertyChanged(nameof(IsChoosing));
            OnPropertyChanged(nameof(ChoosingVisibility));
        }
    }
}
