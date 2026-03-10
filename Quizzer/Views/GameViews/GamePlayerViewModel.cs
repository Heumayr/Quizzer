using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
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

        public QuestionStepResource? QuestionStepResource
        {
            get => questionStepResource;
            set
            {
                questionStepResource = value;
                OnPropertyChanged();
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

            if (viewContent is QuestionStepResource resource)
            {
                QuestionStepResource = resource;
                ShowQuestionView = Visibility.Visible;
            }
            else
            {
                QuestionStepResource = null;
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