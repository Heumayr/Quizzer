using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;

namespace Quizzer.Server
{
    /// <summary>M9: „seine spiele und statistiken sehen" - vom Server gerechnet, nicht lokal.</summary>
    public sealed class StatisticsViewModel : ViewModelBase
    {
        private string summary = "Wird geladen …";

        public sealed record Row(string Spiel, string Gamemaster, string Stand, string Platz, int Punkte, int Richtig, int Falsch, string Gespielt);

        public ObservableCollection<Row> Games { get; } = new();

        /// <summary>Eine Kachel der Bestenliste (M35 C9): Titel und je Platz eine Zeile.</summary>
        public sealed record Kachel(string Titel, IReadOnlyList<string> Zeilen);

        public ObservableCollection<Kachel> Bestenliste { get; } = new();

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

                Bestenliste.Clear();
                foreach (var kachel in Kacheln(stats.Leaderboard))
                    Bestenliste.Add(kachel);

                Summary = $"{stats.PlayerName}: {stats.GamesPlayed} Spiele gespielt, {stats.GamesWon} gewonnen, "
                    + $"{stats.TotalScore} Punkte insgesamt. Als Gamemaster: {stats.GamesAsGameMaster} Spiele.";
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Summary = ex.Message;
            }
        }

        /// <summary>Je Art mit Bestwert eine Kachel, dazu die meisten Klaus; eine leere sagt es, statt zu fehlen.</summary>
        internal static IEnumerable<Kachel> Kacheln(Leaderboard liste)
        {
            foreach (var art in liste.Categories)
                yield return new Kachel($"{art.Title} ({art.KindName})", OderLeer(art.Entries.Select(Zeile)));

            yield return new Kachel("Meiste Klaus",
                OderLeer(liste.MostSteals.Select(s => $"{s.Rank}. {s.PlayerName} – {s.Steals} {(s.Steals == 1 ? "Klau" : "Klaus")}")));
        }

        /// <summary>„1. Anna – 187 ms (Spieleabend, 16.11.2026)" - das Spiel steht dabei, damit man sieht, woher ein Rekord kommt.</summary>
        internal static string Zeile(LeaderboardEntry eintrag)
        {
            var datum = eintrag.RecordedAt is { } am ? ", " + am.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) : string.Empty;
            return $"{eintrag.Rank}. {eintrag.PlayerName} – {eintrag.ValueText} ({eintrag.GameName}{datum})";
        }

        private static IReadOnlyList<string> OderLeer(IEnumerable<string> zeilen)
        {
            var liste = zeilen.ToList();
            return liste.Count == 0 ? ["Noch kein Eintrag."] : liste;
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
