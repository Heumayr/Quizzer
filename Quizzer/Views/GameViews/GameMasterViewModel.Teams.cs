using Quizzer.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.Teams;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// „Teams umsetzen“ zwischen zwei Zellen (M37 T2): derselbe Dialog wie im Spielaufbau, auf dem geladenen Spiel - das
    /// Fragefenster ist modal, während einer Zelle ist der Knopf nicht erreichbar. Alte Stempel bleiben (F-M37-07), die
    /// nächste Zelle stempelt das neue Team. Den Stand an die Telefone (<c>SetTeamsAsync</c>) gibt T4.
    /// </summary>
    public partial class GameMasterViewModel
    {
        public Visibility TeamsVisibility => Game?.IsTeamGame == true ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Knopf sagt, wenn jemand ohne Team ist - etwa wer sich am Telefon dazugemeldet hat (M10).</summary>
        public string TeamsUmsetzenText => Game is { IsTeamGame: true } spiel && Teamzuordnung.OhneTeam(spiel).Count is > 0 and var ohne
            ? $"Teams umsetzen … ({ohne} ohne Team)"
            : "Teams umsetzen …";

        private RelayCommand? teamsUmsetzenCommand;

        public ICommand TeamsUmsetzenCommand => teamsUmsetzenCommand ??= new RelayCommand(_ =>
        {
            if (Game != null && TeamsView.ShowFor(Game, Window, "Teams umsetzen"))
                TeamsAnzeigeAktualisieren();
        });

        /// <summary>Nach dem Umsetzen und nach einem Beitritt am Telefon: den Knopf und die Teamkacheln am Beamer (T3) neu lesen.</summary>
        internal void TeamsAnzeigeAktualisieren()
        {
            OnPropertyChanged(nameof(TeamsUmsetzenText));
            StatsContext.TeamsAktualisieren(IsGameFinished);
        }
    }
}
