using Quizzer.Base;
using Quizzer.Logic.Spielablauf;
using Quizzer.ViewModels;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>Eine Zelle anklicken: Fragefenster oeffnen, danach Punkte, Waehler und Spielstand nachziehen.</summary>
    public partial class GameMasterViewModel
    {
        private ICommand? _cellClickCommand;

        //public ICommand CellClickCommand => _cellClickCommand ??= new RelayCommand<GameGridCoordinateViewModel>(OnCellClicked);
        public ICommand CellClickCommand => _cellClickCommand ??= new AsyncRelayCommand((p) => OnCellClickedAsync(p as GameGridCoordinateViewModel));

        private async Task OnCellClickedAsync(GameGridCoordinateViewModel? cell)
        {
            if (cell == null || Game == null) return;

            var context = new CurrentQuestionViewModel()
            {
                Coordinate = cell.Coordinate,
                GamePlayerViewModel = GamePlayerViewModel,
                GameMasterViewModel = this,
            };

            var qMaster = new QuestionMasterView()
            {
                DataContext = context
            };

            var merk = LetzteWertung.Merke(Game, cell.Coordinate);

            qMaster.ShowDialog();

            // Nur ein echter Zug ersetzt den gemerkten - ansehen und schliessen nimmt die
            // Rücknahme der vorigen Frage nicht weg.
            if (merk.HatSichGeaendert(Game))
                LetzteWertungMerken(merk);

            await NachTeamregelAsync(context, merk); // M37 T5: nach einer bewerteten Teamregel spricht der Nächste

            cell.RefreshFromModel();

            StatsContext.UpdateScores(IsGameFinished);

            if (context.SetNextChoosingPlayer)
            {
                await SetNextChoosingPlayer(context.CoordinateCorrectedAnsweredPlayers);
            }

            await UpdateGameStateAsync();

            // War das die letzte Zelle, wechselt der Spielerbildschirm auf die Siegerehrung.
            // Bis 2026-09-06 geschah das nur, wenn der Spielleiter den Punktestand danach von
            // Hand umschaltete - sonst blieb das leere Raster stehen.
            GamePlayerViewModel?.RefreshFinishState();
        }
    }
}
