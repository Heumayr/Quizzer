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
        /// Zieht Rundenzähler und Anzeige nach und fragt an einer Punkteschwelle nach dem
        /// Phasenwechsel.
        /// <para>
        /// <b>Wartet den Phasenwechsel ab.</b> Bis 2026-09-06 wurde er nur losgeschickt, während
        /// der Aufrufer weiterlief - in <c>LoadModel</c> lief unmittelbar danach ein zweites
        /// <c>VMSaveAsync</c> auf dasselbe Spiel. Zwei gleichzeitige Schreibvorgänge auf einer
        /// Zeile: einer gewinnt, der andere bekommt eine
        /// <c>DbUpdateConcurrencyException</c> - und das Spiel ließ sich genau dann nicht
        /// öffnen, wenn eine Schwelle anstand. Aufgedeckt hat es ein Test, der nur im
        /// Gesamtlauf umfiel.
        /// </para>
        /// <para>
        /// <b>Die Schwelle zählt gespielte Zellen, nicht die Nummer der nächsten Runde.</b>
        /// Verglichen wurde bis 2026-09-07 <c>CurrentRound</c> - also <c>gespielt + 1</c> - mit
        /// den Schwellen aus <c>CalculatetThreshold</c>, und die stehen für eine <i>Anzahl</i>:
        /// bei 24 Fragen und drei Phasen sind das 8 und 16. Der Wechsel kam damit <b>eine Frage
        /// zu früh</b>: Phase 1 bekam 7 Fragen, Phase 3 dafür 9. Bei vier Zellen und zwei Phasen
        /// - dem Fall, den die Zusicherungen bauen - wechselte das Spiel schon <b>nach der
        /// ersten</b> Frage.
        /// </para>
        /// <para>
        /// <b>Der bestehende Test schrieb den Fehler fest</b> („eine von vier Zellen gespielt:
        /// die nächste Runde ist die zweite"). Er ist mitkorrigiert; daneben steht jetzt eine
        /// Zusicherung, die genau den Zustand „eine zu früh" abweist.
        /// </para>
        /// <para>
        /// <b>Hierher verschoben am 2026-09-07</b> aus <c>GameMasterViewModel.cs</c>, die über
        /// der Größen-Obergrenze liegt und nicht länger werden darf.
        /// </para>
        /// </summary>
        private async Task UpdateGameStateAsync()
        {
            if (Game == null) return;

            var vorherigeRunde = Game.CurrentRound;
            var gespielt = GameGridCoordinatesDoneCount;

            CurrentRound = gespielt + 1;

            if (CurrentRound > vorherigeRunde && Game.PhaseTrashholds.Contains(gespielt))
            {
                var advance = UserPrompt.Confirm(
                    "Punkteschwelle erreicht. Zur nächsten Phase wechseln?", "Phasenschwelle");

                if (advance)
                {
                    Game.RaisePhase();
                    await SaveAndRefreshAfterPhaseChangeAsync();
                }
            }

            OnPropertyChanged(nameof(GameGridCoordinatesCount));
            OnPropertyChanged(nameof(GameGridCoordinatesDoneCount));
            OnPropertyChanged(nameof(ProgressHeadline));
            OnPropertyChanged(nameof(OpenCellsText));
            OnPropertyChanged(nameof(PhaseHeadline));
            OnPropertyChanged(nameof(WindowTitle));
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
