using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using System.Collections.ObjectModel;
using System.Windows;

namespace Quizzer.Server
{
    /// <summary>M9: „seine spiele und statistiken sehen" - vom Server gerechnet, nicht lokal.</summary>
    public sealed class StatisticsViewModel : ViewModelBase
    {
        private string summary = "Wird geladen …";

        public sealed record Row(string Spiel, string Gamemaster, string Stand, string Platz, int Punkte, int Richtig, int Falsch, string Gespielt);

        public ObservableCollection<Row> Games { get; } = new();

        public string Summary
        {
            get => summary;
            private set { summary = value; OnPropertyChanged(); }
        }

        protected override async Task OnloadAsync()
        {
            if (ServerSession.Api == null)
            {
                Summary = "Nicht mit dem Server verbunden.";
                return;
            }

            try
            {
                var stats = await ServerSession.Api.GetStatisticsAsync();

                Games.Clear();
                foreach (var g in stats.Games)
                {
                    Games.Add(new Row(
                        g.GameName,
                        g.IsGameMaster ? "ich" : g.GameMasterName,
                        Describe(g.State),
                        g.Rank > 0 ? $"{g.Rank}. von {g.PlayerCount}" : "-",
                        g.FinalScore,
                        g.RightCount,
                        g.WrongCount,
                        $"{g.PlayedCells} / {g.TotalCells}"));
                }

                Summary = $"{stats.PlayerName}: {stats.GamesPlayed} Spiele gespielt, {stats.GamesWon} gewonnen, "
                    + $"{stats.TotalScore} Punkte insgesamt. Als Gamemaster: {stats.GamesAsGameMaster} Spiele.";
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Summary = ex.Message;
            }
        }

        private static string Describe(GameState state) => state switch
        {
            GameState.Building => "in Vorbereitung",
            GameState.InProgress => "läuft",
            GameState.Finished => "beendet",
            _ => state.ToString(),
        };

        public override Task VMSaveAsync() => Task.CompletedTask;
    }
}
