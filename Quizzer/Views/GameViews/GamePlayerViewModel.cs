using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using static Quizzer.Views.HelperViewModels.GridBuilder;

namespace Quizzer.Views.GameViews
{
    public class GamePlayerViewModel : ViewModelBase
    {
        private GameGridVMs gameGridVMs = new();
        private QuestionStepResource? questionStepResource;
        private Visibility showGameGridView = Visibility.Visible;
        private Visibility showQuestionView = Visibility.Hidden;
        private Game? game;

        private QuestionStepViewContext? questionStepViewContext;
        private string questionText = string.Empty;
        private Visibility showPlayerStats = Visibility.Collapsed;

        public Brush HeaderColumnBrush { get; set; } = StaticResources.HeaderColumnImageBrush;
        public Brush HeaderRowBrush { get; set; } = StaticResources.HeaderRowImageBrush;

        public Brush PlayGroundBackGroundBrush => StaticResources.PlayGroundBackGround;

        public GameMasterViewModel? GameMasterViewModel { get; set; }

        public StatsContext StatsContext => GameMasterViewModel?.StatsContext ?? new();

        public bool IsGameFinished => GameMasterViewModel?.IsGameFinished ?? false;

        public Visibility ShowPlayerStats
        {
            get => showPlayerStats;
            set
            {
                showPlayerStats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowMainContent));
                OnPropertyChanged(nameof(ShowBottomStats));
                OnPropertyChanged(nameof(ShowFullscreenStats));
            }
        }

        public Visibility ShowMainContent =>
            ShowPlayerStats == Visibility.Visible && IsGameFinished
                ? Visibility.Collapsed
                : Visibility.Visible;

        public Visibility ShowBottomStats =>
            ShowPlayerStats == Visibility.Visible && !IsGameFinished
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ShowFullscreenStats =>
            ShowPlayerStats == Visibility.Visible && IsGameFinished
                ? Visibility.Visible
                : Visibility.Collapsed;

        public GameGridVMs GameGridVMs
        {
            get => gameGridVMs;
            internal set
            {
                gameGridVMs = value;
                OnPropertyChanged();
                CellVMs = CollectionViewSource.GetDefaultView(value.CellVMs);
                ColumnHeaderVMs = CollectionViewSource.GetDefaultView(value.ColumnHeaderVMs);
                RowHeaderVMs = CollectionViewSource.GetDefaultView(value.RowHeaderVMs);
            }
        }

        public ICollectionView? CellVMs
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView? ColumnHeaderVMs
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView? RowHeaderVMs
        {
            get => field;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public Game? Game
        {
            get => game;
            set
            {
                game = value;
                OnGameModelChanged();
            }
        }

        public Brush BackgroundBrush { get; set; } = Brushes.Wheat;

        public void OnGameModelChanged()
        {
            OnPropertyChanged(nameof(Game));

            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(CellHeight));
            OnPropertyChanged(nameof(CellWidth));
        }

        public int Height
        {
            get => Game?.Height ?? 0;
        }

        public int Width
        {
            get => Game?.Width ?? 0;
        }

        public double CellHeight
        {
            get => Game?.CellHeight ?? 60;
            set
            {
                if (Game == null) return;
                if (Game.CellHeight == value) return;

                Game.CellHeight = value;
                OnPropertyChanged(nameof(CellHeight));
            }
        }

        public double CellWidth
        {
            get => Game?.CellWidth ?? 120;
            set
            {
                if (Game == null) return;
                if (Game.CellWidth == value) return;

                Game.CellWidth = value;
                OnPropertyChanged(nameof(CellWidth));
            }
        }

        public Visibility ShowQuestionText => string.IsNullOrEmpty(QuestionText) ? Visibility.Hidden : Visibility.Visible;

        public string QuestionText
        {
            get => questionText;
            set
            {
                questionText = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(ShowQuestionText));
            }
        }

        public QuestionStepResource? QuestionStepResource
        {
            get => questionStepResource;
            set
            {
                questionStepResource = value;
                OnPropertyChanged();

                if (questionStepResource != null)
                {
                    QuestionText = QuestionStepViewContext?.Question?.QuestionText ?? "";
                    ShowQuestionView = Visibility.Visible;
                }
                else
                {
                    QuestionText = "";
                    ShowQuestionView = Visibility.Hidden;
                }
            }
        }

        public QuestionStepViewContext? QuestionStepViewContext
        {
            get => questionStepViewContext;
            set
            {
                questionStepViewContext = value;
                OnPropertyChanged();

                QuestionStepResource = questionStepViewContext?.Step;
            }
        }

        public Visibility ShowGameGridView
        {
            get => showGameGridView;
            set
            {
                showGameGridView = value;
                OnPropertyChanged();
            }
        }

        public Visibility ShowQuestionView
        {
            get => showQuestionView;
            set
            {
                showQuestionView = value;
                OnPropertyChanged();
            }
        }

        public void SetShowPlayerStats(bool show)
        {
            if (show)
            {
                ShowPlayerStats = Visibility.Visible;
                return;
            }

            ShowPlayerStats = Visibility.Collapsed;

            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(ShowMainContent));
            OnPropertyChanged(nameof(ShowBottomStats));
            OnPropertyChanged(nameof(ShowFullscreenStats));
        }

        public void SetView(object? viewContent)
        {
            if (viewContent == null)
            {
                ShowGameGridView = Visibility.Visible;
                GameGridVMs = GameGridVMs;
            }
            else
            {
                ShowGameGridView = Visibility.Hidden;
            }

            if (viewContent is QuestionStepViewContext context)
            {
                QuestionStepViewContext = context;
            }
            else
            {
                QuestionStepViewContext = null;
                ShowQuestionView = Visibility.Hidden;
            }
        }

        public override async Task VMSaveAsync()
        {
        }

        protected override async Task OnloadAsync()
        {
            OnGameModelChanged();
        }

        #region Current Buzzer Winner

        private CancellationTokenSource? currentBuzzerWinnerResetCts;
        private readonly Dictionary<Window, Window> currentBuzzerWinnerWindows = new();

        private List<Window>? OpenPlayerViewVindows => GameMasterViewModel?.OpenGamePlayerViews;
        private Player? currentBuzzerWinner;

        public Player? CurrentBuzzerWinner
        {
            get => currentBuzzerWinner;
            set
            {
                if (ReferenceEquals(currentBuzzerWinner, value))
                    return;

                currentBuzzerWinner = value;
                OnPropertyChanged();

                if (currentBuzzerWinner != null)
                    ShowCurrentBuzzerWinnerWindows(currentBuzzerWinner);
                else
                    CloseCurrentBuzzerWinnerWindows();

                ScheduleCurrentBuzzerWinnerReset();
            }
        }

        private void ShowCurrentBuzzerWinnerWindows(Player? player)
        {
            if (player == null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                CloseCurrentBuzzerWinnerWindows();

                var owners = OpenPlayerViewVindows?
                    .Where(IsUsableWindow)
                    .Distinct()
                    .ToList();

                if (owners == null || owners.Count == 0)
                    return;

                var statsVm = CreateWinnerStatsViewModel(player);

                foreach (var owner in owners)
                {
                    if (statsVm == null)
                        continue;

                    var content = new UcPlayerStatsView
                    {
                        DataContext = statsVm
                    };

                    var popup = new Window
                    {
                        Owner = owner,
                        WindowStyle = WindowStyle.None,
                        ResizeMode = ResizeMode.NoResize,
                        AllowsTransparency = true,
                        Background = Brushes.Transparent,
                        ShowInTaskbar = false,
                        ShowActivated = false,
                        Topmost = true,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        Content = content
                    };

                    CenterWindowOnOwner(owner, popup);

                    currentBuzzerWinnerWindows[owner] = popup;
                    popup.Show();
                }
            });
        }

        private static bool IsUsableWindow(Window? window)
        {
            if (window == null)
                return false;

            if (window.Dispatcher == null)
                return false;

            if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished)
                return false;

            if (!window.IsLoaded)
                return false;

            return true;
        }

        private static void CenterWindowOnOwner(Window owner, Window popup)
        {
            popup.WindowStartupLocation = WindowStartupLocation.Manual;

            popup.Loaded += (_, __) =>
            {
                var ownerWidth = owner.ActualWidth;
                var ownerHeight = owner.ActualHeight;

                if (ownerWidth <= 0 || ownerHeight <= 0)
                    return;

                popup.Left = owner.Left + (ownerWidth - popup.ActualWidth) / 2;
                popup.Top = owner.Top + (ownerHeight - popup.ActualHeight) / 2;
            };
        }

        private PlayerStatsContext? CreateWinnerStatsViewModel(Player player)
        {
            if (Game == null)
                return null;

            var context = new PlayerStatsContext()
            {
                Game = Game,
                Player = player,
                StatsContext = StatsContext,
                GameMasterViewModel = GameMasterViewModel,
                AlternativText = $"Schnellster{Environment.NewLine}Buzzer! 🏆",
            };

            return context;
        }

        private void CloseCurrentBuzzerWinnerWindows()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var popup in currentBuzzerWinnerWindows.Values.ToList())
                {
                    try
                    {
                        if (popup != null)
                            popup.Close();
                    }
                    catch
                    {
                    }
                }

                currentBuzzerWinnerWindows.Clear();
            });
        }

        private void ScheduleCurrentBuzzerWinnerReset()
        {
            currentBuzzerWinnerResetCts?.Cancel();
            currentBuzzerWinnerResetCts?.Dispose();
            currentBuzzerWinnerResetCts = null;

            if (CurrentBuzzerWinner == null)
                return;

            var cts = new CancellationTokenSource();
            currentBuzzerWinnerResetCts = cts;

            _ = ResetCurrentBuzzerWinnerAsync(cts.Token);
        }

        private async Task ResetCurrentBuzzerWinnerAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        CurrentBuzzerWinner = null;
                });
            }
            catch (TaskCanceledException)
            {
            }
        }

        #endregion Current Buzzer Winner
    }
}