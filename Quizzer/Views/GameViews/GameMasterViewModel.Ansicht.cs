using Quizzer.Base;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>Fenster und Ansicht: Spieleransicht, Punktestand dort, Vollbild, Raster oder Punkte.</summary>
    public partial class GameMasterViewModel
    {
        private AsyncRelayCommand? openGamePlayerViewCommand;
        public ICommand OpenGamePlayerViewCommand => openGamePlayerViewCommand ??= new AsyncRelayCommand(OpenGamePlayerViewAsync);

        private async Task OpenGamePlayerViewAsync(object? commandParameter)
        {
            // Ein zweites Spieleransichts-Fenster teilt sich das ViewModel mit dem ersten und
            // ueberschreibt dessen Fensterverweis - danach folgt nur noch eines dem Spiel, und
            // welches, ist Zufall. Stattdessen das vorhandene nach vorne holen.
            var vorhanden = OpenGamePlayerViews.FirstOrDefault(w => w.IsLoaded);

            if (vorhanden != null)
            {
                vorhanden.Activate();
                return;
            }

            var window = new GamePlayerView()
            {
                DataContext = GamePlayerViewModel
            };

            window.ContentRendered += (s, e) =>
            {
                WindowsForMediaHandle.Add(window);
                MediaPreviewCoordinator.SetOwnerWindows(WindowsForMediaHandle);
            };

            OpenGamePlayerViews.Add(window);

            window.Show();
        }

        /// <summary>
        /// Beschriftung des Knopfes, der den Punktestand bei den Spielern ein- und ausblendet.
        /// Sie sagt, was der Druck bewirkt - nicht, was gerade gilt.
        /// </summary>
        public string ToggleStatsButtonText
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        } = "Punktestand ausblenden";

        // Voreingestellt sichtbar, ebenso wie im GamePlayerViewModel: der Punktestand ist die
        // Auskunft, nach der die Mitspieler am haeufigsten fragen.
        private bool showStats = true;

        private RelayCommand? togglePlayerStatsViewCommand;
        public ICommand TogglePlayerStatsViewCommand => togglePlayerStatsViewCommand ??= new RelayCommand(TogglePlayerStatsView);

        private void TogglePlayerStatsView(object? commandParameter)
        {
            showStats = !showStats;
            ToggleStatsButtonText = showStats ? "Punktestand ausblenden" : "Punktestand einblenden";
            GamePlayerViewModel.SetShowPlayerStats(showStats);
        }

        /// <summary>Was auf dem Knopf steht: wohin der Punktestand als Naechstes wandert.</summary>
        public string StatsPlacementButtonText
            => GamePlayerViewModel?.ScoresLeft == true ? "Punktestand nach unten" : "Punktestand nach links";

        private RelayCommand? toggleStatsPlacementCommand;

        /// <summary>
        /// Schaltet den Punktestand zwischen „links untereinander" und „unten nebeneinander" um
        /// und merkt sich die Wahl fuer das naechste Spiel (Nutzerwunsch 2026-10-21).
        /// </summary>
        public ICommand ToggleStatsPlacementCommand => toggleStatsPlacementCommand ??= new RelayCommand(_ =>
        {
            if (GamePlayerViewModel == null)
                return;

            GamePlayerViewModel.ScoresLeft = !GamePlayerViewModel.ScoresLeft;

            try
            {
                DataModels.UserSettings.SaveScoresLeft(GamePlayerViewModel.ScoresLeft);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                // Nicht merken koennen ist kein Grund, die Umstellung zu verweigern.
            }

            OnPropertyChanged(nameof(StatsPlacementButtonText));
        });

        private int statsRowHeight = 1;

        public string StatsRowHeight
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        } = "*";

        private RelayCommand? changeViewCommand;
        public ICommand ChangeViewCommand => changeViewCommand ??= new RelayCommand(ChangeView);

        private void ChangeView(object? commandParameter)
        {
            if (statsRowHeight == 1)
            {
                statsRowHeight = 4;
            }
            else
            {
                statsRowHeight = 1;
            }

            StatsRowHeight = $"{statsRowHeight}*";
        }

        private RelayCommand? toggleFullScreenCommand;
        public ICommand ToggleFullScreenCommand => toggleFullScreenCommand ??= new RelayCommand(ToggleFullScreen);

        public bool IsFullScreen { get; private set; } = false;

        private void ToggleFullScreen(object? commandParameter)
        {
            IsFullScreen = !IsFullScreen;

            foreach (var win in WindowsForMediaHandle)
            {
                if (win is not WindowBase winbase) continue;

                winbase.SetFullscreen(IsFullScreen);
            }

            MediaPreviewCoordinator.FullScreenPreviewEnabled = IsFullScreen;
        }
    }
}
