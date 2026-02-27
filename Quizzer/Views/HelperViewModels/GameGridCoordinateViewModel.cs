using System;
using System.ComponentModel;
using Quizzer.Base;
using Quizzer.DataModels.Models;

namespace Quizzer.ViewModels
{
    public class GameGridCoordinateViewModel : ViewModelBase
    {
        private GameGridCoordinate _coordinate;

        private INotifyPropertyChanged? _questionNotify;

        public GameGridCoordinateViewModel(GameGridCoordinate coordinate)
        {
            _coordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
            HookQuestion(_coordinate.Question);
        }

        public GameGridCoordinate Coordinate
        {
            get => _coordinate;
            set
            {
                if (ReferenceEquals(_coordinate, value) || value is null) return;

                UnhookQuestion(_coordinate.Question);
                _coordinate = value;
                HookQuestion(_coordinate.Question);

                OnPropertyChanged(nameof(Coordinate));
                RaiseAllPropertiesChanged();
            }
        }

        public int GridRow => Y + 1;
        public int GridColumn => X + 1;

        public int X
        {
            get => _coordinate.X;
            set
            {
                if (_coordinate.X == value) return;
                _coordinate.X = value;
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(GridColumn));
                OnPropertyChanged(nameof(DisplayBuild));
            }
        }

        public int Y
        {
            get => _coordinate.Y;
            set
            {
                if (_coordinate.Y == value) return;
                _coordinate.Y = value;
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(GridRow));
                OnPropertyChanged(nameof(DisplayBuild));
            }
        }

        public int Z
        {
            get => _coordinate.Z;
            set
            {
                if (_coordinate.Z == value) return;
                _coordinate.Z = value;
                OnPropertyChanged(nameof(Z));
            }
        }

        public int Phase
        {
            get => _coordinate.Phase;
            set
            {
                if (_coordinate.Phase == value) return;
                _coordinate.Phase = value;
                OnPropertyChanged(nameof(Phase));
            }
        }

        public Guid QuestionsId
        {
            get => _coordinate.QuestionId;
            set
            {
                if (_coordinate.QuestionId == value) return;
                _coordinate.QuestionId = value;
                OnPropertyChanged(nameof(QuestionsId));
            }
        }

        public bool IsDone
        {
            get => _coordinate.IsDone;
            set
            {
                if (_coordinate.IsDone == value) return;
                _coordinate.IsDone = value;
                OnPropertyChanged(nameof(IsDone));
            }
        }

        public QuestionBase? Question
        {
            get => _coordinate.Question;
            set
            {
                if (ReferenceEquals(_coordinate.Question, value)) return;

                UnhookQuestion(_coordinate.Question);
                _coordinate.Question = value;
                _coordinate.CalculatedPoints();
                HookQuestion(_coordinate.Question);

                OnPropertyChanged(nameof(Question));
                OnPropertyChanged(nameof(DisplayBuild));
                OnPropertyChanged(nameof(DisplayPlay));
            }
        }

        public string DisplayBuild => _coordinate.DisplayBuild;
        public string DisplayPlay => _coordinate.DisplayPlay;

        public string DifficultyDisplay => _coordinate.Question?.Difficulty.ToString() ?? string.Empty;

        public string PointsDisplay => _coordinate != null ? $"{_coordinate?.CurrentPoints} pts / -{_coordinate?.CurrentMinusPoints} pts" : string.Empty;

        /// <summary>
        /// Call this if you changed the underlying model outside this VM and want to refresh bindings.
        /// </summary>
        public void RefreshFromModel() => RaiseAllPropertiesChanged();

        private void RaiseAllPropertiesChanged()
        {
            // WPF treats string.Empty as "everything changed"
            OnPropertyChanged(string.Empty);
        }

        private void HookQuestion(QuestionBase? q)
        {
            if (q is INotifyPropertyChanged npc)
            {
                _questionNotify = npc;
                _questionNotify.PropertyChanged += Question_PropertyChanged;
            }
        }

        private void UnhookQuestion(QuestionBase? q)
        {
            if (_questionNotify != null)
            {
                _questionNotify.PropertyChanged -= Question_PropertyChanged;
                _questionNotify = null;
            }
        }

        private void Question_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Question.Designation or Question.Points changes should update these computed strings
            OnPropertyChanged(nameof(DisplayBuild));
            OnPropertyChanged(nameof(DisplayPlay));
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}