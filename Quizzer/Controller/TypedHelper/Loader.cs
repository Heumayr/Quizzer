using Quizzer.Datamodels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Text;

namespace Quizzer.Controller.TypedHelper
{
    public static class Loader
    {
        private static ObservableCollection<Category> categories = new ObservableCollection<Category>();

        private static ObservableCollection<QuestionBase> questions = new ObservableCollection<QuestionBase>();

        public static ObservableCollection<Category> Categories { get => categories; set => categories = value; }
        public static ObservableCollection<QuestionBase> Questions { get => questions; set => questions = value; }

        public static ObservableCollection<Player> Players { get; set; } = new ObservableCollection<Player>();

        public static ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();

        public static async Task ReloadAllAsync()
        {
            await LoadCategoriesAsync();
            await LoadQuestionsAsync();
            await LoadPlayersAsync();
            await LoadGamesAsync();
        }

        public static async Task<ObservableCollection<Category>> LoadCategoriesAsync()
        {
            var ctrl = new GenericDataHandler();
            var categories = (await ctrl.LoadFromFileAsync<Category>()).ToList();

            Categories = new ObservableCollection<Category>(categories);

            return Categories;
        }

        public static async Task<ObservableCollection<QuestionBase>> LoadQuestionsAsync()
        {
            if (!Categories.Any())
            {
                await LoadCategoriesAsync();
            }

            var ctrl = new GenericDataHandler();
            var questions = (await ctrl.LoadFromFileAsync<QuestionBase>()).ToList();

            foreach (var q in questions)
            {
                if (q.CategoryId != Guid.Empty)
                    q.Category = Categories.FirstOrDefault(c => c.Id == q.CategoryId);
            }

            Questions = new ObservableCollection<QuestionBase>(questions.OrderBy(q => q.Category?.Designation).ThenBy(q => q.Difficulty).ThenBy(q => q.Points));

            return Questions;
        }

        public static async Task<ObservableCollection<Player>> LoadPlayersAsync()
        {
            var ctrl = new GenericDataHandler();
            var players = (await ctrl.LoadFromFileAsync<Player>()).ToList();

            Players = new ObservableCollection<Player>(players);

            return Players;
        }

        public static async Task<ObservableCollection<Player>> LoadGamesAsync()
        {
            var ctrl = new GenericDataHandler();

            var games = (await ctrl.LoadFromFileAsync<Game>()).ToList();

            foreach (var g in games)
            {
                g.Players = Players.Where(p => g.PlayerIds.Contains(p.Id)).ToList();
            }

            Games = new ObservableCollection<Game>(games);

            return Players;
        }
    }
}