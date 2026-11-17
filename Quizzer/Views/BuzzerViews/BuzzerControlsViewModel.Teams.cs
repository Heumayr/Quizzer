using Quizzer.Base;
using Quizzer.Views.StaticRessources;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Die Team-Sperre im Fragefenster (M37 T4, F-M37-09): wer bei dieser Frage schon gebuzzert hat, steht hier, und die
    /// Spielleitung kann das Team wieder freigeben (E1). Geleert wird sie beim Schließen der Frage.
    /// </summary>
    public partial class BuzzerControlsViewModel
    {
        private IReadOnlyCollection<Guid> GesperrteTeams => BuzzerController?.StateManager.TeamBuzzLock.LockedTeams ?? [];

        public Visibility TeamSperreVisibility => GesperrteTeams.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>„Schon gebuzzert: Rot, Blau“ - ohne Team steht der Spieler selbst da.</summary>
        public string TeamSperreText => "Schon gebuzzert: " + string.Join(", ", GesperrteTeams.Select(NameDesTeams));

        private string NameDesTeams(Guid key)
            => Game?.GameTeams.FirstOrDefault(t => t.Id == key)?.Name
               ?? Game?.Players.FirstOrDefault(p => p.Id == key)?.CalculatedDisplayName
               ?? "?";

        private AsyncRelayCommand? teamFreigebenCommand;

        public ICommand TeamFreigebenCommand => teamFreigebenCommand ??= new AsyncRelayCommand(_ => GibTeamsFreiAsync(), _ => GesperrteTeams.Count > 0);

        /// <summary>„Team wieder freigeben“: die gesperrten Teams dürfen bei dieser Frage wieder buzzern.</summary>
        public async Task GibTeamsFreiAsync()
        {
            try
            {
                if (BuzzerController is null)
                    return;

                await BuzzerController.ReleaseTeamsAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }

            TeamSperreGeaendert();
        }

        /// <summary>Die Frage ist zu: die Sperre geht, ohne zu senden - das tut das Zurücksetzen danach.</summary>
        public void ClearTeamLock()
        {
            BuzzerController?.StateManager.TeamBuzzLock.Clear();
            TeamSperreGeaendert();
        }

        internal void TeamSperreGeaendert()
        {
            OnPropertyChanged(nameof(TeamSperreVisibility));
            OnPropertyChanged(nameof(TeamSperreText));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
