using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Questions;
using Quizzer.Logic.Controller.TypedControllers;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// „Leere Zellen füllen" (M32, 2026-10-23): schlägt für jede leere Zelle eine Frage vor -
    /// Kategorie nach der Spaltenüberschrift, leicht oben, schwer unten - und belegt erst nach
    /// der Rückfrage. Die Regeln stehen in <see cref="SpielfeldFueller"/>.
    /// </summary>
    public partial class EditGameViewModel
    {
        private AsyncRelayCommand? fillEmptyCellsCommand;

        public ICommand FillEmptyCellsCommand => fillEmptyCellsCommand ??= new AsyncRelayCommand(FillEmptyCellsAsync, _ => IsBuilding);

        private async Task FillEmptyCellsAsync(object? _)
        {
            if (Game == null || !IsBuilding)
                return;

            List<DataModels.Models.QuestionBase> fragen;
            List<DataModels.Models.Base.Category> kategorien;

            using (var ctrl = new QuestionBasesController())
                fragen = QuestionOwnership.VisibleTo(await ctrl.GetAllAsync()).ToList();

            using (var ctrl = new CategoriesController())
                kategorien = (await ctrl.GetAllAsync()).ToList();

            // Für die Spalten ohne passende Überschrift: welche Kategorie dort schon liegt.
            var nachId = fragen.ToDictionary(q => q.Id);
            foreach (var zelle in Game.GameGridCoordinates.Where(z => z.QuestionBase == null && z.QuestionBaseId is { } id && nachId.ContainsKey(id)))
                zelle.QuestionBase = nachId[zelle.QuestionBaseId!.Value];

            var vorschlag = SpielfeldFueller.Vorschlag(Game, fragen, kategorien);
            var hinweise = vorschlag.Hinweise.Count > 0 ? "\n\n" + string.Join("\n", vorschlag.Hinweise) : string.Empty;

            if (vorschlag.Belegungen.Count == 0)
            {
                UserPrompt.Inform("Für keine leere Zelle gibt es eine passende freie Frage. Die Spaltenüberschrift muss wie eine Kategorie heißen - oder in der Spalte liegt schon eine Frage."
                    + hinweise, "Leere Zellen füllen");
                return;
            }

            var spalten = vorschlag.Belegungen.Select(b => b.Zelle.X).Distinct().Count();
            if (!UserPrompt.Confirm($"{vorschlag.Belegungen.Count} leere Zellen in {spalten} Spalte(n) mit passenden Fragen belegen? Leichte oben, schwere unten. Jede Zelle lässt sich danach wie gewohnt ändern."
                    + hinweise, "Leere Zellen füllen"))
                return;

            foreach (var (zelle, frage) in vorschlag.Belegungen)
            {
                zelle.QuestionBaseId = frage.Id;
                zelle.QuestionBase = frage;
                await SaveCoordinateAsync(zelle);
            }

            await RebuildCellsAsync();
        }
    }
}
