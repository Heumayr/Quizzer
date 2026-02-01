using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class EditQuestionViewModel : ViewModelBase
    {
        public Array Difficulties { get; } = Enum.GetValues(typeof(Difficulty));

        public ObservableCollection<Category> Categories { get => Loader.Categories; set => Loader.Categories = value; }

        public Category? SelectedCategory
        {
            get
            {
                return _question?.Category;
            }

            set
            {
                _question?.Category = value;

                if (value != null)
                {
                    _question?.CategoryId = value.Id;
                }
                OnModelChanged();
            }
        }

        public List<QuestionStepResource> SelectedSteps { get; set; } = new List<QuestionStepResource>();

        public EditResultState ResultState { get; set; } = EditResultState.Cancelled;

        private QuestionBase? _question;

        public QuestionBase? Question
        {
            get => _question;
            set
            {
                if (!Equals(_question, value))
                {
                    _question = value;
                    OnModelChanged();
                }
            }
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(Question));
        }

        public void SetQuestionBase(QuestionBase questionBase)
        {
            Question = questionBase;

            OnPropertyChanged(nameof(QuestionBase));
        }

        private AsyncRelayCommand? saveCommand;
        private Category? selectedCategory;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            if (Question == null)
            {
                return;
            }

            if (Question.Id == Guid.Empty)
            {
                Question.Id = Guid.NewGuid();
                ResultState = EditResultState.New;
                Loader.Questions.Add(Question);
            }
            else
            {
                ResultState = EditResultState.Updated;
            }

            var ctrl = new GenericDataHandler();

            await ctrl.SaveToFileAsync(Loader.Questions);
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }

        private AsyncRelayCommand? addStepCommnad;
        public ICommand AddStepCommnad => addStepCommnad ??= new AsyncRelayCommand(AddStepCommnadAsync);

        private Task AddStepCommnadAsync(object? commandParameter)
        {
            if (Question == null)
            {
                throw new InvalidOperationException("Question is null");
            }

            var step = new QuestionStepResource();

            Question.Steps.Add(step);

            return EditStepAsync(step);
        }

        private async Task EditStepAsync(QuestionStepResource step)
        {
            var window = new EditStepView();

            if (window.DataContext is EditStepViewModel vm)
            {
                vm.Step = step;
                window.ShowDialog();

                OnDatagridSourceChanged();
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        private void OnDatagridSourceChanged()
        {
            if (Question == null)
            {
                return;
            }

            CollectionViewSource.GetDefaultView(Question.Steps)?.Refresh();
        }

        private AsyncRelayCommand? openStepCommand;
        public ICommand OpenStepCommand => openStepCommand ??= new AsyncRelayCommand(OpenStepAsync);

        private Task OpenStepAsync(object? commandParameter)
        {
            if (commandParameter is QuestionStepResource step)
            {
                return EditStepAsync(step);
            }

            return Task.CompletedTask;
        }
    }
}