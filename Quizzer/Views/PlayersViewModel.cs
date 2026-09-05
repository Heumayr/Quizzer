using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class PlayersViewModel : ViewModelBase
    {
        private ObservableCollection<Player> players = new();

        private ObservableCollection<Player> Players
        {
            get => players;
            set
            {
                players = value;
                OnPropertyChanged();
                PlayersView = CollectionViewSource.GetDefaultView(players);
                OnPropertyChanged(nameof(PlayersView));
            }
        }

        public ICollectionView? PlayersView { get; private set; }

        public override async Task VMSaveAsync()
        {
            using var ctrl = new PlayersController();

            foreach (var p in Players)
            {
                await ctrl.UpsertAsync(p);
            }

            await ctrl.SaveChangesAsync();
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        private async Task SaveCommandAsync(object? param)
        {
            await VMSaveAsync();
            await OnloadAsync();
        }

        protected override async Task OnloadAsync()
        {
            using var ctrl = new PlayersController();
            var players = await ctrl.GetAllAsync();
            Players = new ObservableCollection<Player>(players);
        }

        public ObservableCollection<Player> SelectedPlayers { get; set; } = new();

        private AsyncRelayCommand? openPlayerCommand;
        public ICommand OpenPlayerCommand => openPlayerCommand ??= new AsyncRelayCommand(OpenPlayerAsync);

        private async Task OpenPlayerAsync(object? model)
        {
            if (model is Player player)
            {
                await EditPlayerAsync(player);
            }
        }

        private AsyncRelayCommand? addPlayerCommand;
        public ICommand AddPlayerCommand => addPlayerCommand ??= new AsyncRelayCommand((p) => EditPlayerAsync(new Player()));

        private async Task EditPlayerAsync(Player player)
        {
            var window = new EditPlayerView();

            if (window.DataContext is EditPlayerViewModel vm)
            {
                vm.SetPlayer(player);
                window.ShowDialog();

                await OnloadAsync();
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        private AsyncRelayCommand? removePlayerCommand;

        public ICommand RemovePlayerCommand => removePlayerCommand ??= new AsyncRelayCommand(RemovePlayerAsync);

        /// <summary>
        /// Entfernt die ausgewählten Mitspieler - und nennt vorher, was daran hängt.
        /// <para>
        /// <c>QuestionResult.PlayerId</c> steht in der Datenbank auf CASCADE: mit dem Mitspieler
        /// verschwindet <b>seine gesamte Punktehistorie aus allen Spielen</b>, ohne Meldung und
        /// ohne Weg zurück. Bis 2026-09-06 fragte die Rückfrage nur „wirklich entfernen?" - das
        /// klingt nach einer Zeile in einer Liste. Jetzt steht die Zahl der Ergebniszeilen dabei.
        /// </para>
        /// <para>
        /// Die Rückfrage kommt außerdem erst nach der Prüfung auf Auswahl; vorher fragte sie
        /// auch dann, wenn gar nichts ausgewählt war.
        /// </para>
        /// </summary>
        private async Task RemovePlayerAsync(object? commandParameter)
        {
            if (SelectedPlayers == null || SelectedPlayers.Count == 0)
            {
                return;
            }

            var toRemove = new List<Player>(SelectedPlayers);

            if (!await ConfirmRemovalAsync(toRemove))
                return;

            using var ctrl = new PlayersController();

            foreach (var player in toRemove)
            {
                await ctrl.DeleteAsync(player.Id);
            }

            await ctrl.SaveChangesAsync();

            await OnloadAsync();
        }

        /// <summary>
        /// Fragt nach und beziffert dabei, wie viele Ergebniszeilen mitgelöscht würden.
        /// </summary>
        private static async Task<bool> ConfirmRemovalAsync(List<Player> toRemove)
        {
            int ergebnisse;

            using (var ctrlResults = new QuestionResultsController())
            {
                ergebnisse = await ctrlResults.CountResultsOfPlayersAsync(toRemove.Select(p => p.Id));
            }

            var namen = string.Join(", ", toRemove.Select(p => p.CalculatedDisplayName));

            var zeilen = ergebnisse == 1 ? "1 Ergebniszeile" : $"{ergebnisse} Ergebniszeilen";

            var frage = ergebnisse == 0
                ? $"{namen} entfernen?"
                : $"{namen} entfernen?" + Environment.NewLine + Environment.NewLine
                  + $"Dabei wird die gesamte Punktehistorie mitgelöscht: {zeilen} aus allen "
                  + "Spielen. Das lässt sich nicht rückgängig machen.";

            return UserPrompt.Confirm(frage, "Mitspieler entfernen");
        }
    }
}