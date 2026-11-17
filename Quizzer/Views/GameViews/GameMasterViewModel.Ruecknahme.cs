using Quizzer.Base;
using Quizzer.Logic.Spielablauf;
using Quizzer.Views.StaticRessources;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>Die letzte Wertung zuruecknehmen (V5) - eine Stufe, mit Rueckfrage.</summary>
    public partial class GameMasterViewModel
    {
        private LetzteWertung? letzteWertung;

        private void LetzteWertungMerken(LetzteWertung? merk)
        {
            letzteWertung = merk;
            OnPropertyChanged(nameof(UndoToolTip));
            CommandManager.InvalidateRequerySuggested();
        }

        public string UndoToolTip => letzteWertung?.Zelle.QuestionBase?.Designation is { Length: > 0 } frage
            ? $"Nimmt die Wertung für „{frage}“ zurück - Punkte, erledigt, wer auswählt. Eine Stufe."
            : "Nimmt die Wertung der zuletzt gespielten Frage zurück. Noch nichts gewertet.";

        private AsyncRelayCommand? undoCommand;

        /// <summary>V5: die letzte Wertung zurücknehmen - mit Rückfrage, die sagt, wer was verliert.</summary>
        public ICommand UndoCommand => undoCommand ??= new AsyncRelayCommand(UndoAsync, _ => letzteWertung != null);

        private async Task UndoAsync(object? _)
        {
            if (Game == null || letzteWertung is not { } merk)
                return;

            if (!UserPrompt.Confirm(merk.Beschreibung(Game), "Wertung zurücknehmen"))
                return;

            try
            {
                await merk.NimmZurueckAsync(Game);
                await VMSaveAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
                return;
            }

            LetzteWertungMerken(null);

            GameGridVMs.CellVMs.FirstOrDefault(c => c.Coordinate == merk.Zelle)?.RefreshFromModel();
            StatsContext.UpdateScores(IsGameFinished);
            ChoosingPlayerChanged();
            await UpdateGameStateAsync();
            GamePlayerViewModel?.RefreshFinishState();
        }
    }
}
