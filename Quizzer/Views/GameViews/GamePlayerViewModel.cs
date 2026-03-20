using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews;
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
        private Visibility showQuestionText;

        private string backgroundImagePath = Settings.BackgroundImagePath;
        public Brush HeaderBrush { get; set; } = Brushes.Black;

        public string BackgroundImagePath
        {
            get => backgroundImagePath;
            set
            {
                backgroundImagePath = value;
                OnPropertyChanged();
            }
        }

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
    }
}