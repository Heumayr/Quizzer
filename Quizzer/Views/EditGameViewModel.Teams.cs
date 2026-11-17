using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.Teams;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Teamspiel im Spielaufbau (M37 T2): der Schalter, Beratungszeit und Wertung der Minispiele stehen am Spiel und
    /// gehen mit „Speichern“; Teams und Zuteilung bearbeitet der eigene Dialog und schreibt sie selbst - unter der Sperre
    /// des Rasteraufbaus (B26). Eigene Teildatei, weil <c>EditGameViewModel.cs</c> nicht wachsen darf.
    /// </summary>
    public partial class EditGameViewModel
    {
        public bool IsTeamGame
        {
            get => Game?.IsTeamGame ?? false;
            set
            {
                if (Game == null || Game.IsTeamGame == value)
                    return;

                Game.IsTeamGame = value;

                // Altbestand steht aus der Migration auf 0 - eingeschaltet gilt die Vorgabe.
                if (value && Game.TeamCouncilSeconds == 0)
                    Game.TeamCouncilSeconds = TeamGrenzen.DefaultCouncilSeconds;

                TeamsAktualisieren();
            }
        }

        /// <summary>Beratungszeit der Teamberatung in Sekunden, auf 0 bis 600 begrenzt; 0 heißt ohne Countdown.</summary>
        public int TeamCouncilSeconds
        {
            get => Game?.TeamCouncilSeconds ?? 0;
            set
            {
                if (Game == null)
                    return;

                Game.TeamCouncilSeconds = Math.Clamp(value, 0, TeamGrenzen.MaxCouncilSeconds);
                OnPropertyChanged();
            }
        }

        public bool TeamMinigameAverage
        {
            get => Game?.TeamMinigameScoring == TeamMinigameScoring.Average;
            set => SetzeTeamWertung(value, TeamMinigameScoring.Average);
        }

        public bool TeamMinigameSum
        {
            get => Game?.TeamMinigameScoring == TeamMinigameScoring.Sum;
            set => SetzeTeamWertung(value, TeamMinigameScoring.Sum);
        }

        /// <summary>Zwei Optionsfelder an einem Wert: nur das gesetzte schreibt, das abgewählte meldet bloß <c>false</c>.</summary>
        private void SetzeTeamWertung(bool gewaehlt, TeamMinigameScoring wertung)
        {
            if (!gewaehlt || Game == null)
                return;

            Game.TeamMinigameScoring = wertung;
            OnPropertyChanged(nameof(TeamMinigameAverage));
            OnPropertyChanged(nameof(TeamMinigameSum));
        }

        public Visibility TeamEinstellungenVisibility => IsTeamGame ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Eine Zeile neben dem Schalter: wie viele Teams, und ob jemand ohne Team ist.</summary>
        public string TeamUebersicht
        {
            get
            {
                if (Game == null || !Game.IsTeamGame)
                    return string.Empty;

                var teams = Game.GameTeams.Count switch { 0 => "noch keine Teams", 1 => "1 Team", var n => $"{n} Teams" };
                var teilnehmer = Teamzuordnung.Teilnehmer(Game).Count;
                var ohne = Teamzuordnung.OhneTeam(Game).Count;

                if (teilnehmer == 0)
                    return $"{teams} · Mitspieler kommen beim Start dazu";

                return ohne == 0 ? $"{teams} · alle zugeteilt" : $"{teams} · {ohne} ohne Team";
            }
        }

        private AsyncRelayCommand? editTeamsCommand;

        public ICommand EditTeamsCommand => editTeamsCommand ??= new AsyncRelayCommand(_ =>
        {
            if (Game != null)
                TeamsView.ShowFor(Game, Window, "Teams bearbeiten", RunGuardedAsync);

            TeamsAktualisieren();
            return Task.CompletedTask;
        }, _ => IsTeamGame);

        /// <summary>Nach Laden, Zurücksetzen und dem Dialog: die Bindungen des Teamblocks neu lesen.</summary>
        private void TeamsAktualisieren()
        {
            OnPropertyChanged(nameof(IsTeamGame));
            OnPropertyChanged(nameof(TeamCouncilSeconds));
            OnPropertyChanged(nameof(TeamMinigameAverage));
            OnPropertyChanged(nameof(TeamMinigameSum));
            OnPropertyChanged(nameof(TeamEinstellungenVisibility));
            OnPropertyChanged(nameof(TeamUebersicht));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
