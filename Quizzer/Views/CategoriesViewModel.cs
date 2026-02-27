using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.DataModels.Models;
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

        public ObservableCollection<Category> Categories { get => Loader.Categories; set => Loader.Categories = value; }

        public Task SaveCategoriesAsync()
        {
            var ctrl = new GenericDataHandler();
            return ctrl.SaveToFileAsync(Categories);
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