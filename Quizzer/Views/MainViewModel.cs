using Quizzer.Base;
using Quizzer.Controller.TypedHelper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class MainViewModel : ViewModelBase
    {
        public override async Task InitializeAsync()
        {
            await Loader.ReloadAllAsync();
        }

        private RelayCommand? startQuizCommand;
        public ICommand StartQuizCommand => startQuizCommand ??= new RelayCommand(StartQuiz);

        private void StartQuiz(object? param)
        {
        }

        private RelayCommand? categoryCommand;
        public ICommand CategoryCommand => categoryCommand ??= new RelayCommand(Categories);

        private void Categories(object? param)
        {
            var window = new CategoriesView();
            window.ShowDialog();
        }

        private RelayCommand? playersCommand;
        public ICommand PlayersCommand => playersCommand ??= new RelayCommand(Players);

        private void Players(object? param)
        {
            var window = new PlayersView();
            window.ShowDialog();
        }

        private RelayCommand? questionsCommand;
        public ICommand QuestionsCommand => questionsCommand ??= new RelayCommand(Questions);

        private void Questions(object? param)
        {
            var window = new QuestionsView();
            window.ShowDialog();
        }

        private RelayCommand? gamesCommand;
        public ICommand GamesCommand => gamesCommand ??= new RelayCommand(Games);

        private void Games(object? commandParameter)
        {
            var window = new GamesView();
            window.ShowDialog();
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}