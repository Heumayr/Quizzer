using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class CategoriesViewModel : ViewModelBase
    {
        public CategoriesViewModel()
        {
        }

        public ObservableCollection<Category> Categories { get; set; } = new();

        public async Task SaveCategoriesAsync()
        {
            using var ctrl = new CategoriesController();

            foreach (var category in Categories)
            {
                await ctrl.UpsertAsync(category);
            }

            await ctrl.SaveChangesAsync();
        }

        protected override async Task Onload()
        {
            Categories.Clear();

            using var ctrl = new CategoriesController();
            var categories = await ctrl.GetAllAsync();

            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? param)
        {
            await VMSaveAsync();
        }

        public override Task VMSaveAsync()
        {
            return SaveCategoriesAsync();
        }
    }
}