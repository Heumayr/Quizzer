using Quizzer.Base;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.StaticRessources;
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
            //await Loader.ReloadAllAsync();
        }

        protected override Task Onload() => Task.CompletedTask;

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
            try
            {
                this.Window?.Hide();
                var window = new GamesView();
                window.Closed += (_, __) => this.Window?.Show();
                window.Show();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
        }

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}