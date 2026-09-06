using Quizzer.Base;

using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public partial class GamesViewModel : ViewModelBase
    {
        public ICollectionView? GamesView { get; set; }

        public ObservableCollection<Game> Games
        {
            get => games;
            set
            {
                games = value;
                OnPropertyChanged();
                GamesView = CollectionViewSource.GetDefaultView(games);
                OnPropertyChanged(nameof(GamesView));
            }
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }

        //private AsyncRelayCommand? saveCommand;
        //public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        //private async Task SaveCommandAsync(object? param)
        //{
        //    await VMSaveAsync();
        //}

        protected override async Task OnloadAsync()
        {
            using var ctrl = new GamesController();
            Games = new ObservableCollection<Game>(await ctrl.GetAllAsync());
        }

        public ObservableCollection<Game> SelectedGames { get; set; } = new();

        private AsyncRelayCommand? openGameCommand;
        /// <summary>
        /// Der Doppelklick auf eine Zeile - er <b>startet</b> das Spiel.
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06:</b> „doppelklick auf spiel startet ... links
        /// daneben ein button für edit ... also nicht mit doppelklick in den edit". Vorher fuehrte
        /// er in den Spielaufbau - der haeufigere Griff am Quizabend ist aber das Starten.
        /// </para>
        /// </summary>
        public ICommand OpenGameCommand => openGameCommand ??= new AsyncRelayCommand(StartGameAsync);

        private AsyncRelayCommand? editGameCommand;

        /// <summary>Der Knopf in der Zeile - er fuehrt in den Spielaufbau.</summary>
        public ICommand EditGameCommand => editGameCommand ??= new AsyncRelayCommand(EditGameFromRowAsync);

        private async Task EditGameFromRowAsync(object? model)
        {
            var game = WelchesSpiel(model);

            if (game == null)
                return;

            await EditGameAsync(game);
        }

        /// <summary>
        /// Auf welches Spiel sich ein Befehl bezieht: die hereingereichte Zeile, sonst die
        /// Auswahl.
        /// <para>
        /// Beide Wege kommen vor - der Doppelklick und der Knopf in der Zeile reichen die Zeile
        /// herein, die Knoepfe links tun es nicht. Wer den Parameter ignoriert, trifft beim
        /// Doppelklick auf eine nicht ausgewaehlte Zeile das falsche Spiel.
        /// </para>
        /// </summary>
        internal Game? WelchesSpiel(object? commandParameter)
            => commandParameter as Game ?? SelectedGames.FirstOrDefault();

        private AsyncRelayCommand? addGameCommand;
        public ICommand AddGameCommand => addGameCommand ??= new AsyncRelayCommand((p) => EditGameAsync(new Game()));

        private async Task EditGameAsync(Game game)
        {
            try
            {
                var window = new EditGameView();

                if (window.DataContext is EditGameViewModel vm)
                {
                    await vm.LoadModel(game.Id);
                    this.Window?.Hide();
                    window.Closed += OnClosed;
                    window.ShowDialog();

                    await OnloadAsync();
                }
                else
                {
                    throw new InvalidOperationException("DataContext is not of type EditGameViewModel");
                }
            }
            catch (Exception)
            {
                this.Window?.Show();
                addGameCommand?.RaiseCanExecuteChanged();
                openGameCommand?.RaiseCanExecuteChanged();
                throw;
            }
            finally
            {
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            this.Window?.Show();
            addGameCommand?.RaiseCanExecuteChanged();
            openGameCommand?.RaiseCanExecuteChanged();
        }

        public void OnDatagridSourceChanged()
        {
            CollectionViewSource.GetDefaultView(Games)?.Refresh();
        }

        private AsyncRelayCommand? removeGameCommand;
        private ObservableCollection<Game> games = new();

        public ICommand RemoveGameCommand => removeGameCommand ??= new AsyncRelayCommand(RemoveGameAsync);

        private async Task RemoveGameAsync(object? commandParameter)
        {
            if (SelectedGames == null || SelectedGames.Count == 0)
            {
                return;
            }

            var toRemove = new List<Game>(SelectedGames);

            if (!await BestaetigeEntfernenAsync(toRemove))
                return;

            using var ctrl = new GamesController();
            foreach (var game in toRemove)
            {
                await ctrl.DeleteAsync(game.Id);
            }

            await ctrl.SaveChangesAsync();
            await OnloadAsync();
        }

        /// <summary>
        /// Fragt nach und beziffert dabei, was mit dem Spiel verschwindet.
        /// <para>
        /// <b>Bis 2026-09-06 stand hier nur „Die ausgewählten Spiele wirklich entfernen?"</b> -
        /// nicht einmal, welche. Mit dem Spiel geht aber der <b>gesamte Punktestand des Abends</b>:
        /// <c>GamesController.BeforeActionAsync</c> räumt Ergebnisse, Zellen, Kopfzeilen und
        /// Zuordnungen selbst weg, weil die Fremdschlüssel auf NO ACTION stehen. Es gibt keinen
        /// Weg zurück.
        /// </para>
        /// <para>
        /// Dieselbe Bezifferung gibt es beim Entfernen eines Mitspielers und einer Frage; das
        /// Spiel war der dritte und letzte unbezifferte Weg.
        /// </para>
        /// </summary>
        private static async Task<bool> BestaetigeEntfernenAsync(List<Game> toRemove)
        {
            int ergebnisse;

            using (var ctrl = new QuestionResultsController())
            {
                ergebnisse = await ctrl.CountResultsOfGamesAsync(toRemove.Select(g => g.Id));
            }

            var namen = string.Join(", ", toRemove.Select(g => g.Designation));

            var frage = $"{namen} entfernen?";

            if (ergebnisse > 0)
            {
                var zeilen = ergebnisse == 1 ? "1 Ergebniszeile" : $"{ergebnisse} Ergebniszeilen";

                frage += Environment.NewLine + Environment.NewLine
                       + $"Dabei geht der gesamte Punktestand dieses Abends verloren: {zeilen}, "
                       + "dazu das Spielfeld und die Zuordnung der Mitspieler. Das lässt sich "
                       + "nicht rückgängig machen.";
            }

            return UserPrompt.Confirm(frage, "Spiel entfernen");
        }

        private AsyncRelayCommand? startGameCommand;
        public ICommand StartGameCommand => startGameCommand ??= new AsyncRelayCommand(StartGameAsync);

        /// <summary>
        /// Startet ein Spiel. Der Doppelklick reicht die Zeile herein, der Knopf links nicht -
        /// dann gilt die Auswahl.
        /// </summary>
        private async Task StartGameAsync(object? commandParameter)
        {
            var game = WelchesSpiel(commandParameter);

            if (game == null || game.Id == Guid.Empty) return;

            try
            {
                this.Window?.Hide();

                var gameView = new GameMasterView();
                var gameContext = new GameMasterViewModel();
                gameView.DataContext = gameContext;

                var loadedGame = await gameContext.LoadModel(game.Id);

                if (loadedGame == null)
                {
                    this.Window?.Show();
                    return;
                }

                gameView.Closed += async (_, __) =>
                {
                    this.Window?.Show();
                };

                gameView.Show();
            }
            catch (Exception)
            {
                this.Window?.Show();
                throw;
            }
            finally
            {
            }
        }
    }
}