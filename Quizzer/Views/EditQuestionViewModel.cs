using Quizzer.Base;

using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
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

        public ObservableCollection<Category> Categories { get; set; } = new();

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

        protected override async Task Onload()
        {
            Categories.Clear();
            using var catCtrl = new CategoriesController();

            var categories = await catCtrl.GetAllAsync();

            foreach (var item in categories)
            {
                Categories.Add(item);
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
            OnDatagridSourceChanged();
        }

        public void SetQuestionBase(QuestionBase questionBase)
        {
            Question = questionBase;

            OnPropertyChanged(nameof(QuestionBase));
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        public override async Task VMSaveAsync()
        {
            if (Question == null) return;

            using var ctrl = new QuestionBasesController();
            var result = await ctrl.UpsertAsync(Question);
            await ctrl.SaveChangesAsync();

            ResultState = result.Created ? EditResultState.New : EditResultState.Updated;
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

            step.SquenceNumber = Question.Steps.Any() ? Question.Steps.Max(q => q.SquenceNumber) + 10 : 0;

            Question.Steps.Add(step);

            return EditStepAsync(step);
        }

        private AsyncRelayCommand? removeStepCommnad;
        public ICommand RemoveStepCommnad => removeStepCommnad ??= new AsyncRelayCommand(RemoveStepCommnadAsync);

        private async Task RemoveStepCommnadAsync(object? commandParameter)
        {
            if (Question == null)
            {
                throw new InvalidOperationException("Question is null");
            }

            foreach (var step in SelectedSteps)
            {
                Question.Steps.Remove(step);
            }

            //await SaveAsync(null);
            OnDatagridSourceChanged();
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

            var view = CollectionViewSource.GetDefaultView(Question.Steps);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(
                new System.ComponentModel.SortDescription(
                    nameof(QuestionStepResource.SquenceNumber),
                    System.ComponentModel.ListSortDirection.Ascending));

            view.Refresh();
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