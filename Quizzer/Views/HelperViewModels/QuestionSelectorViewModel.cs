using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views.HelperViewModels
{
    public class QuestionSelectorViewModel : ViewModelBase
    {
        public Game? Game { get; set; }

        public QuestionBase[] AllQuestion { get; set; } = [];

        public GameGridCoordinate? Coordinate { get; set; }

        public QuestionBase? SelectedQuestion
        {
            get => Coordinate?.QuestionBase;
            set
            {
                Coordinate?.QuestionBase = value;
                Coordinate?.QuestionBaseId = value != null ? value.Id : default;
                OnPropertyChanged(nameof(SelectedQuestion));
                OnPropertyChanged(nameof(CurrentSelectedQuestionDisplay));
            }
        }

        protected override async Task OnloadAsync()
        {
            using var qCtrl = new QuestionBasesController();
            AllQuestion = await qCtrl.GetAllAsync();
        }

        public string CurrentSelectedQuestionDisplay => Coordinate?.QuestionBase != null ? $"{Coordinate.QuestionBase.Category?.Designation} {Coordinate.QuestionBase.Designation} {Coordinate.QuestionBase.Difficulty} {Coordinate.QuestionBase.Points}" : "No question selected";

        public void SetDependencys(Game game, GameGridCoordinate coordinate)
        {
            Game = game;
            Coordinate = coordinate;
            OnModelChanged();
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(AvailableQuestions));
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(Coordinate));
            OnPropertyChanged(nameof(SelectedQuestion));
            OnPropertyChanged(nameof(CurrentSelectedQuestionDisplay));
        }

        public override async Task VMSaveAsync()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<QuestionBase> AvailableQuestions
        {
            get
            {
                var used = Game?.GameGridCoordinates
                    .Select(c => c.QuestionBaseId)
                    .Where(id => id != Guid.Empty)
                    .ToHashSet() ?? new HashSet<Guid>();

                var choices = AllQuestion
                    .Where(q => q.Id != Guid.Empty && !used.Contains(q.Id))
                    .ToList();

                if (SelectedQuestion != null && !choices.Contains(SelectedQuestion))
                {
                    choices.Prepend(SelectedQuestion);
                }
                return choices;
            }
        }

        private AsyncRelayCommand? openQuestionCommand;
        public ICommand OpenQuestionCommand => openQuestionCommand ??= new AsyncRelayCommand(OpenQuestionAsync);

        private async Task OpenQuestionAsync(object? model)
        {
            if (model is QuestionBase questionBase)
            {
                await EditQuestionAsync(questionBase);
            }
        }

        private AsyncRelayCommand? addQuestionCommand;
        public ICommand AddQuestionCommand => addQuestionCommand ??= new AsyncRelayCommand((p) => EditQuestionAsync(new DefaultQuestion()));

        private async Task EditQuestionAsync(QuestionBase questionBase)
        {
            var window = new EditQuestionsView();

            if (window.DataContext is EditQuestionViewModel vm)
            {
                await vm.SetModel(questionBase);
                window.ShowDialog();

                if (vm.ResultState == EditResultState.New || vm.ResultState == EditResultState.Updated || vm.ResultState == EditResultState.Deleted)
                {
                    SelectedQuestion = vm.Question;
                    OnDatagridSourceChanged();
                }
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        public void OnDatagridSourceChanged()
        {
            //CollectionViewSource.GetDefaultView(AvailableQuestions)?.Refresh();
            OnPropertyChanged(nameof(AvailableQuestions));
        }

        private RelayCommand? closeCommand;
        public ICommand CloseCommand => closeCommand ??= new RelayCommand(Close);

        private void Close(object? commandParameter)
        {
            Window?.Close();
        }

        private AsyncRelayCommand? editSelectedQuestionCommand;
        public ICommand EditSelectedQuestionCommand => editSelectedQuestionCommand ??= new AsyncRelayCommand(EditSelectedQuestion);

        private Task EditSelectedQuestion(object? commandParameter)
        {
            if (SelectedQuestion is null)
            {
                return Task.CompletedTask;
            }

            return EditQuestionAsync(SelectedQuestion);
        }

        private RelayCommand? deselectCommand;
        public ICommand DeselectCommand => deselectCommand ??= new RelayCommand(Deselect);

        private void Deselect(object? commandParameter)
        {
            SelectedQuestion = null;
        }

        private AsyncRelayCommand? selectAndCloseCommand;
        public ICommand SelectAndCloseCommand => selectAndCloseCommand ??= new AsyncRelayCommand(SelectAndCloseAsync);

        private Task SelectAndCloseAsync(object? commandParameter)
        {
            if (commandParameter is QuestionBase questionBase)
            {
                if (SelectedQuestion?.Id != questionBase.Id)
                {
                    SelectedQuestion = questionBase;
                }

                Window?.Close();
            }

            return Task.CompletedTask;
        }
    }
}