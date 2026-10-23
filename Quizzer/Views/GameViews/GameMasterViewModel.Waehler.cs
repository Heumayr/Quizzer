using Quizzer.DataModels.Models.Base;

namespace Quizzer.Views.GameViews
{
    /// <summary>Wer als Nächstes die Frage wählt - auch am Spielerbildschirm (M30).</summary>
    public partial class GameMasterViewModel
    {
        /// <summary>Der Mitspieler, der gerade wählt, oder <c>null</c>.</summary>
        public Player? ChoosingPlayerItem => Game == null ? null : Players.FirstOrDefault(p => p.Id == Game.CurrentChoosingPlayerId);

        /// <summary>
        /// Nach jedem Wechsel des Wählers: das Spielleiterfenster, die Punktekacheln (dort und am
        /// Beamer) und der Hinweis über dem Raster des Beamers erfahren es. Ohne diesen Ruf
        /// zeigten Kachel und Beamer weiter den vorigen.
        /// </summary>
        private void ChoosingPlayerChanged()
        {
            OnPropertyChanged(nameof(ChoosingPlayer));
            OnPropertyChanged(nameof(ChoosingPlayerItem));

            foreach (var kachel in StatsContext.PlayerStatsContextList)
                kachel.WaehlerGeaendert();

            GamePlayerViewModel?.WaehlerGeaendert();
        }
    }
}
