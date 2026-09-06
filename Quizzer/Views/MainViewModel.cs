using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class MainViewModel : ViewModelBase
    {
        public override async Task InitializeAsync()
        {
            //await Loader.ReloadAllAsync();
        }

        protected override Task OnloadAsync() => Task.CompletedTask;

        /// <summary>
        /// Wer angemeldet ist - die Zeile im Startfenster.
        /// <para>
        /// <b>B38.</b> Die Anmeldung entscheidet, welche Fragen sichtbar sind und wem eine neue
        /// Frage gehört, und danach stand es in keinem einzigen Fenster. Wer sich vergriff, sah
        /// seine eigenen Fragen nicht und kam nur über einen Neustart zurück.
        /// </para>
        /// </summary>
        public string AngemeldetAls => Session.CurrentModerator is { } leiter
            ? $"Angemeldet als {leiter.CalculatedDisplayName}"
            : "Nicht angemeldet";

        private RelayCommand? switchModeratorCommand;

        /// <summary><b>B04, B06.</b> Die Spielleitung im laufenden Betrieb übergeben.</summary>
        public ICommand SwitchModeratorCommand =>
            switchModeratorCommand ??= new RelayCommand(SwitchModerator);

        /// <summary>
        /// Wie das Anmeldefenster für den Wechsel geöffnet wird. Gekapselt wie
        /// <c>IUserPrompt</c>: ein Fenster bliebe im Testlauf stehen.
        /// </summary>
        public static Func<bool> SwitchModeratorHandler { get; set; } = StandardWechsel;

        /// <summary>Setzt auf das echte Fenster zurueck.</summary>
        public static void ResetSwitchModeratorHandler() => SwitchModeratorHandler = StandardWechsel;

        private static bool StandardWechsel()
        {
            var fenster = new LoginView();

            if (fenster.DataContext is not LoginViewModel vm)
                return false;

            vm.IstWechsel = true;

            fenster.ShowDialog();

            return vm.SignedIn;
        }

        /// <summary>
        /// <b>Ausdrücklich kein <c>Session.SignOut()</c> davor.</b> Wird der Wechsel
        /// abgebrochen, bliebe sonst niemand angemeldet - und der Fragenfilter ließe schlagartig
        /// den gesamten Bestand durch, ohne dass irgendetwas darauf hinweist.
        /// </summary>
        private void SwitchModerator(object? _)
        {
            if (!SwitchModeratorHandler())
                return;

            OnPropertyChanged(nameof(AngemeldetAls));
        }

        private RelayCommand? startQuizCommand;
        public ICommand StartQuizCommand => startQuizCommand ??= new RelayCommand(StartQuiz);

        private void StartQuiz(object? param)
        {
        }

        private RelayCommand? categoryCommand;
        public ICommand CategoryCommand => categoryCommand ??= new RelayCommand(Categories);

        private void Categories(object? param)
        {
            var window = new CategoriesView();
            window.ShowDialog();
        }

        private RelayCommand? playersCommand;
        public ICommand PlayersCommand => playersCommand ??= new RelayCommand(Players);

        private void Players(object? param)
        {
            var window = new PlayersView();
            window.ShowDialog();
        }

        private RelayCommand? questionsCommand;
        public ICommand QuestionsCommand => questionsCommand ??= new RelayCommand(Questions);

        private void Questions(object? param)
        {
            var window = new QuestionsView();
            window.ShowDialog();
        }

        private RelayCommand? settingsCommand;
        public ICommand SettingsCommand => settingsCommand ??= new RelayCommand(OpenSettings);

        /// <summary>
        /// Datenordner und Datenbank. Nach dem Speichern wirken die Werte sofort fuer alles, was
        /// danach geladen wird - ein Neustart ist nur noetig, wenn die Datenbank gewechselt hat.
        /// </summary>
        private void OpenSettings(object? param)
        {
            var window = new SettingsView();

            window.ShowDialog();
        }

        private RelayCommand? gamesCommand;
        public ICommand GamesCommand => gamesCommand ??= new RelayCommand(Games);

        private void Games(object? commandParameter)
        {
            try
            {
                this.Window?.Hide();
                var window = new GamesView();
                window.Closed += (_, __) => this.Window?.Show();
                window.Show();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}