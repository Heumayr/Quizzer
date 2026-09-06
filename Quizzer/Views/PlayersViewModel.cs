using Quizzer.Base;
using Quizzer.DataModels;
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
        /// Weist ab, wenn die Person ein Spiel leitet, und fragt sonst nach - mit dem, was dabei
        /// verloren geht.
        /// <para>
        /// <b>B48.</b> Bis hierher zählte die Rückfrage nur Ergebniszeilen und löschte danach
        /// ungeprüft. <c>Game.ModeratorPlayerId</c> steht aber auf NO ACTION - wer eine Person
        /// entfernt, die ein Spiel leitet, bekam einen rohen Fremdschlüsselfehler zu sehen,
        /// <b>nach</b> der bestätigten Rückfrage. Dieselbe Abweisung gibt es bei den Fragen
        /// seit Längerem (<c>QuestionsViewModel.ConfirmRemovalAsync</c>).
        /// </para>
        /// </summary>
        private static async Task<bool> ConfirmRemovalAsync(List<Player> toRemove)
        {
            // Sich selbst zu entfernen ist der Weg in einen Zustand ohne Ausweg: Session zeigt
            // danach auf eine geloeschte Kennung, und die naechste neue Frage traegt sie als
            // Besitzer ein - das INSERT verletzt seit dem 2026-09-06 den Fremdschluessel
            // FK_QuestionBase_Player_OwnerPlayerId. Der Spielleiter saehe einen rohen
            // Datenbankfehler, und nur ein Neustart brachte ihn heraus.
            var selbst = toRemove.FirstOrDefault(p => p.Id == Session.CurrentModeratorId);

            if (selbst != null)
            {
                UserPrompt.Inform(
                    $"{selbst.CalculatedDisplayName} ist gerade selbst als Spielleitung "
                    + "angemeldet und lässt sich deshalb nicht entfernen."
                    + Environment.NewLine + Environment.NewLine
                    + "Zuerst im Startfenster die Spielleitung wechseln.",
                    "Mitspieler entfernen");

                return false;
            }

            using (var ctrlSpiele = new GamesController())
            {
                var leitet = new List<string>();

                foreach (var player in toRemove)
                {
                    var spiele = await ctrlSpiele.GameNamesModeratedByAsync(player.Id);

                    if (spiele.Count > 0)
                        leitet.Add($"{player.CalculatedDisplayName} - leitet: {string.Join(", ", spiele)}");
                }

                if (leitet.Count > 0)
                {
                    UserPrompt.Inform(
                        "Diese Mitspieler leiten ein Spiel und lassen sich nicht entfernen:"
                        + Environment.NewLine + Environment.NewLine
                        + string.Join(Environment.NewLine, leitet)
                        + Environment.NewLine + Environment.NewLine
                        + "Zuerst im Spielaufbau eine andere Spielleitung eintragen.",
                        "Mitspieler entfernen");

                    return false;
                }
            }

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