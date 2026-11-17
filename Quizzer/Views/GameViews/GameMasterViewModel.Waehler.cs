using Quizzer.DataModels.Models.Base;

namespace Quizzer.Views.GameViews
{
    /// <summary>Wer als Nächstes die Frage wählt - auch am Spielerbildschirm (M30).</summary>
    public partial class GameMasterViewModel
    {
        public string ChoosingPlayer => GetChoosingPlayerSignature();

        private string GetChoosingPlayerSignature()
        {
            var result = "Noch nicht festgelegt";

            if (Game == null) return result;

            var player = Players.FirstOrDefault(p => p.Id == Game.CurrentChoosingPlayerId);

            if (player != null)
            {
                result = $"{player.Designation}";
                if (string.IsNullOrEmpty(player.DisplayName) == false)
                {
                    result += $"{Environment.NewLine}{player.DisplayName}";
                }
            }

            return result;
        }

        public void InitChoosingPlayer()
        {
            if (Game == null) return;

            var players = Players.ToArray();
            var playerCount = Players.Count();

            if (Game.RegularChoosingPlayerId == Guid.Empty && playerCount > 0)
            {
                var random = new Random();
                Game.RegularChoosingPlayerId = players[random.Next(playerCount)].Id;
            }

            if (Game.CurrentChoosingPlayerId == Guid.Empty)
                Game.CurrentChoosingPlayerId = Game.RegularChoosingPlayerId;

            ChoosingPlayerChanged();
        }

        public async Task SetNextChoosingPlayer(List<Player> cellWinners)
        {
            if (Game == null)
                return;

            if (cellWinners.Count > 0)
            {
                SetChoosingPlayerFromWinners(cellWinners);
            }
            else
            {
                SetNextRegularChoosingPlayer();
            }

            await VMSaveAsync();
            ChoosingPlayerChanged();
        }

        private void SetChoosingPlayerFromWinners(List<Player> winners)
        {
            if (Game == null)
                return;

            if (winners.Count == 1)
            {
                Game.CurrentChoosingPlayerId = winners[0].Id;
                return;
            }

            var currentWinnerStillValid = winners.Any(w => w.Id == Game.CurrentChoosingPlayerId);
            if (currentWinnerStillValid)
                return;

            var random = new Random();
            Game.CurrentChoosingPlayerId = winners[random.Next(winners.Count)].Id;
        }

        private void SetNextRegularChoosingPlayer()
        {
            if (Game == null)
                return;

            var players = Players.ToArray();
            if (players.Length == 0)
                return;

            var currentIndex = Array.FindIndex(players, p => p.Id == Game.RegularChoosingPlayerId);

            if (currentIndex == -1)
            {
                Game.CurrentChoosingPlayerId = players[0].Id;
                return;
            }

            var nextIndex = (currentIndex + 1) % players.Length;
            Game.CurrentChoosingPlayerId = players[nextIndex].Id;
            Game.RegularChoosingPlayerId = Game.CurrentChoosingPlayerId;
        }

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
