using Newtonsoft.Json;
using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
using Quizzer.Datamodels.QuestionTypes;
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
        public ObservableCollection<QuestionBase> Questions { get => Loader.Questions; set => Loader.Questions = value; }

        public Task SaveAsync()
        {
            var ctrl = new GenericDataHandler();
            return ctrl.SaveToFileAsync(Questions);
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveCommandAsync);

        private async Task SaveCommandAsync(object? param)
        {
            await SaveAsync();
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
        public ICommand AddQuestionCommand => addQuestionCommand ??= new AsyncRelayCommand((p) => EditQuestionAsync(new DefaultQuestion()));

        private async Task EditQuestionAsync(QuestionBase questionBase)
        {
            var window = new EditQuestionsView();

            if (window.DataContext is EditQuestionViewModel vm)
            {
                vm.SetQuestionBase(questionBase);
                window.ShowDialog();

                if (vm.ResultState == EditResultState.New || vm.ResultState == EditResultState.Updated || vm.ResultState == EditResultState.Deleted)
                {
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
            CollectionViewSource.GetDefaultView(Questions)?.Refresh();
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

            foreach (var question in toRemove)
            {
                Questions.Remove(question);
            }

            await SaveAsync();
            OnDatagridSourceChanged();

            return;
        }
    }
}