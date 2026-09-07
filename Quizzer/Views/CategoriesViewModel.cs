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

        /// <summary>
        /// Schreibt die Liste. <b>Leere Zeilen werden übergangen.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> das Raster legt eine neue Zeile schon beim Hineinklicken
        /// an (<c>CanUserAddRows</c>), und eine ohne Bezeichnung wurde anstandslos gespeichert.
        /// Sie stand danach als <b>leerer Eintrag in der Kategorieauswahl jeder Frage</b> —
        /// dieselbe Familie wie der Mitspieler ohne Namen, der in der Anmeldung vorgewählt war.
        /// </para>
        /// <para>
        /// <b>Übergangen, nicht abgewiesen:</b> die leere Zeile entsteht durch bloßes Klicken,
        /// nicht durch eine Absicht. Eine Rückfrage darauf wäre Lärm.
        /// </para>
        /// </summary>
        public async Task SaveCategoriesAsync()
        {
            using var ctrl = new CategoriesController();

            foreach (var category in Categories)
            {
                if (string.IsNullOrWhiteSpace(category.Designation))
                    continue;

                await ctrl.UpsertAsync(category);
            }

            await ctrl.SaveChangesAsync();
        }

        protected override async Task OnloadAsync()
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