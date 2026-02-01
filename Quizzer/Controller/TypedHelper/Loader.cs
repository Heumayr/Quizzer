using Quizzer.Datamodels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Quizzer.Controller.TypedHelper
{
    public static class Loader
    {
        private static ObservableCollection<Category> categories = new ObservableCollection<Category>();
        private static ObservableCollection<QuestionBase> questions = new ObservableCollection<QuestionBase>();

        public static ObservableCollection<Category> Categories { get => categories; set => categories = value; }
        public static ObservableCollection<QuestionBase> Questions { get => questions; set => questions = value; }

        public static async Task ReloadAllAsync()
        {
            await LoadCategoriesAsync();
            await LoadQuestionsAsync();
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

            Questions = new ObservableCollection<QuestionBase>(questions);

            return Questions;
        }
    }
}