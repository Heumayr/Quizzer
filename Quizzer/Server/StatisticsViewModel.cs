using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Views.Bestenliste;
using System.Collections.ObjectModel;
using System.Windows;

namespace Quizzer.Server
{
    /// <summary>M9: „seine spiele und statistiken sehen" - vom Server gerechnet, nicht lokal.</summary>
    public sealed class StatisticsViewModel : ViewModelBase
    {
        private string summary = "Wird geladen …";

        public sealed record Row(string Spiel, string Gamemaster, string Stand, string Platz, string Team, int Punkte, int Richtig, int Falsch, string Gespielt);

        public ObservableCollection<Row> Games { get; } = new();

        /// <summary>Die Kacheln der Bestenliste (M35 C9), gebaut wie in der Bestenliste ohne Server (M37 E03).</summary>
        public ObservableCollection<BestenlisteKachel> Bestenliste { get; } = new();

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
                    Games.Add(Zeile(g));

                Bestenliste.Clear();
                foreach (var kachel in BestenlisteKacheln.Aus(stats.Leaderboard))
                    Bestenliste.Add(kachel);

                Summary = Zusammenfassung(stats);
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Summary = ex.Message;
            }
        }

        internal static Row Zeile(GameStatistic g) => new(
            g.GameName,
            g.IsGameMaster ? "ich" : g.GameMasterName,
            Describe(g.State),
            g.Rank > 0 ? $"{g.Rank}. von {g.PlayerCount}" : "-",
            g.TeamName is { } team ? $"{team}: {g.TeamRank}. von {g.TeamCount}" : "-",
            g.FinalScore,
            g.RightCount,
            g.WrongCount,
            $"{g.PlayedCells} / {g.TotalCells}");

        /// <summary>
        /// Die Kopfzeile. Mit Teamspielen nennt sie Einzel- und Teamsiege getrennt (M37 T12, F-M37-14): ein Einzelsieg zählt nur
        /// ohne Teams; ein älterer Server sendet keine Teams, dann bleibt der Satz wie vorher.
        /// </summary>
        internal static string Zusammenfassung(StatisticsResponse stats)
        {
            var siege = stats.TeamWins > 0 || stats.Games.Any(g => g.TeamName != null)
                ? $"{stats.GamesWon} ohne Teams gewonnen, {stats.TeamWins} mit dem Team gewonnen, "
                : $"{stats.GamesWon} gewonnen, ";

            return $"{stats.PlayerName}: {stats.GamesPlayed} Spiele gespielt, {siege}"
                + $"{stats.TotalScore} Punkte insgesamt. Als Gamemaster: {stats.GamesAsGameMaster} Spiele.";
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
