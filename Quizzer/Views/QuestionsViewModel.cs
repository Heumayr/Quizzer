using Newtonsoft.Json;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.QuestionTemplates.Helper;
using Quizzer.Views.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    internal class QuestionsViewModel : ViewModelBase
    {
        public Array QuestionTypes { get; } = Enum.GetValues(typeof(QuestionType));

        public QuestionType SelectedQuestionType { get; set; }

        private ObservableCollection<QuestionBase> _questions = new();

        public ObservableCollection<QuestionBase> Questions
        {
            get => _questions;
            private set
            {
                _questions = value;
                OnPropertyChanged();
                QuestionsView = CollectionViewSource.GetDefaultView(_questions);
                OnPropertyChanged(nameof(QuestionsView));
            }
        }

        public ICollectionView? QuestionsView { get; private set; }

        public override async Task VMSaveAsync()
        {
            using var ctrl = new QuestionBasesController();

            foreach (var q in Questions)
            {
                await ctrl.UpsertAsync(q);
            }

            await ctrl.SaveChangesAsync();
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        private async Task SaveCommandAsync(object? param)
        {
            await VMSaveAsync();
        }

        protected override async Task OnloadAsync()
        {
            using var ctrl = new QuestionBasesController();
            var questions = await ctrl.GetAllAsync();

            Questions = new ObservableCollection<QuestionBase>(questions);
        }

        public ObservableCollection<QuestionBase> SelectedQuestions { get; set; } = new();

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
        public ICommand AddQuestionCommand => addQuestionCommand ??= new AsyncRelayCommand((p) => EditQuestionAsync(Factory.CreateNewQuestion(SelectedQuestionType)));

        private async Task EditQuestionAsync(QuestionBase questionBase)
        {
            var window = new EditQuestionsView();

            if (window.DataContext is EditQuestionViewModel vm)
            {
                await vm.SetModel(questionBase);
                window.ShowDialog();

                await OnloadAsync();
            }
            else
            {
                throw new InvalidOperationException("DataContext is not of type EditQuestionViewModel");
            }
        }

        private AsyncRelayCommand? removeQuestionCommand;
        public ICommand RemoveQuestionCommand => removeQuestionCommand ??= new AsyncRelayCommand(RemoveQuestionAsync);

        private async Task RemoveQuestionAsync(object? commandParameter)
        {
            if (SelectedQuestions == null || SelectedQuestions.Count == 0)
            {
                return;
            }

            var toRemove = new List<QuestionBase>(SelectedQuestions);

            using var ctrl = new QuestionBasesController();

            foreach (var question in toRemove)
            {
                await ctrl.DeleteAsync(question.Id);
            }

            await ctrl.SaveChangesAsync();
            await OnloadAsync();
        }
    }
}