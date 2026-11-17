using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.Views.GameViews
{
    /// <summary>Die Punktekacheln je Mitspieler und der Beitritt am Telefon (M10).</summary>
    public partial class GameMasterViewModel
    {
        private void OnPlayerJoined(object? sender, Player player)
        {
            if (Game == null)
                return;

            InitStatContext(Game);
            OnPropertyChanged(nameof(Players));

            // M37 T2: im Teamspiel ist der Neue ohne Team - der Knopf „Teams umsetzen“ sagt es.
            TeamsAnzeigeAktualisieren();
        }

        private void InitStatContext(Game game)
        {
            StatsContext.PlayerStatsContextList.Clear();
            StatsContext.Game = game;
            StatsContext.GameMasterViewModel = this;

            if (game.Players.Count() == 0)
            {
                return;
            }

            foreach (var player in game.Players)
            {
                var context = new PlayerStatsContext()
                {
                    Game = game,
                    Player = player,
                    StatsContext = StatsContext,
                    GameMasterViewModel = this
                };

                StatsContext.PlayerStatsContextList.Add(context);
            }

            StatsContext.UpdateScores(IsGameFinished);
        }
    }
}
