using Quizzer.Base;
using Quizzer.Views.StaticRessources;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Die Phasensteuerung des Spielabends.
    /// <para>
    /// Eigene Teildatei, weil <c>GameMasterViewModel.cs</c> ueber der Groessen-Obergrenze liegt
    /// und durch eine Aenderung nicht laenger werden darf (standards-allgemein.md §5).
    /// </para>
    /// </summary>
    public partial class GameMasterViewModel
    {
        private AsyncRelayCommand? raiseGamePhaseCommand;

        public ICommand RaiseGamePhaseCommand =>
            raiseGamePhaseCommand ??= new AsyncRelayCommand(RaiseGamePhaseAsync);

        private async Task RaiseGamePhaseAsync(object? commandParameter)
        {
            if (Game == null) return;

            Game.RaisePhase();

            await SaveAndRefreshAfterPhaseChangeAsync();
        }

        private AsyncRelayCommand? lowerGamePhaseCommand;

        public ICommand LowerGamePhaseCommand =>
            lowerGamePhaseCommand ??= new AsyncRelayCommand(LowerGamePhaseAsync);

        private async Task LowerGamePhaseAsync(object? commandParameter)
        {
            if (Game == null) return;

            Game.LowerPhase();

            await SaveAndRefreshAfterPhaseChangeAsync();
        }

        private async Task SetPhaseAndSetCoordinatesPhaseAsync(object? commandParameter)
        {
            if (Game == null) return;

            Game.SetPhaseAndSetCoordinatesPhase(Game.Phase);

            await SaveAndRefreshAfterPhaseChangeAsync();
        }

        /// <summary>
        /// Startet den Phasenwechsel aus einem synchronen Zusammenhang heraus.
        /// <para>
        /// <c>UpdateGameState</c> ist synchron und wird an mehreren Stellen gerufen; der Wechsel
        /// muss dort also losgeschickt statt abgewartet werden. Bis 2026-09-06 stand hier ein
        /// blosses <c>_ =</c>: scheiterte das Schreiben, verschwand der Fehler in einer
        /// unbeobachteten Aufgabe, und der Spielleiter sah eine neue Phase, die in der Datenbank
        /// nicht stand. Jetzt faengt der Aufruf selbst.
        /// </para>
        /// </summary>
        private void StartPhaseChangeSave()
        {
            _ = SaveAndRefreshAfterPhaseChangeAsync();
        }

        /// <summary>
        /// Schreibt den Phasenwechsel und zieht die Zellen nach. Wirft nicht: der Aufruf kommt
        /// teils aus einem synchronen Zusammenhang, in dem niemand faengt.
        /// </summary>
        private async Task SaveAndRefreshAfterPhaseChangeAsync()
        {
            try
            {
                await VMSaveAsync();

                foreach (var cell in GameGridVMs.CellVMs)
                {
                    cell.RefreshFromModel();
                }
            }
            catch (Exception ex)
            {
                UserPrompt.Inform(
                    "Der Phasenwechsel liess sich nicht speichern. Das Spielfeld zeigt die neue "
                    + "Phase, in der Datenbank steht noch die alte."
                    + Environment.NewLine + Environment.NewLine + ex.Message,
                    "Phasenwechsel");
            }

            OnPropertyChanged(nameof(GamePhase));
            OnPropertyChanged(nameof(PhaseHeadline));
        }
    }
}
