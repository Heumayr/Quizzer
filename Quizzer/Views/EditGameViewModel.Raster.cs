using Quizzer.Base;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Rasteransicht des Spielaufbaus: mit welcher Phase die Kachelpunkte gerechnet werden.
    /// <para>
    /// Eigene Teildatei, weil <c>EditGameViewModel.cs</c> über der Größen-Obergrenze liegt und
    /// durch eine Änderung nicht länger werden darf.
    /// </para>
    /// </summary>
    public partial class EditGameViewModel
    {
        private int testPhase = 1;

        /// <summary>
        /// Die Phase, mit der das Raster zur Ansicht gerechnet wird.
        /// <para>
        /// <b>Vorbelegt, nicht null.</b> Bis 2026-09-07 stand hier keine Vorbelegung - das Feld
        /// zeigte 0, und ein Klick auf „Raster neu aufbauen" setzte jede Kachel auf
        /// „0 / −0 Punkte", weil der Phasenteil der Formel mit <c>(Faktor · Phase)</c>
        /// multipliziert und bei Phase 0 alles wegnimmt. Ein anschließendes Speichern schrieb
        /// die Nullen fest.
        /// </para>
        /// </summary>
        public int TestPhase
        {
            get => testPhase;
            set => testPhase = value < 1 ? 1 : value;
        }

        private AsyncRelayCommand? refreshGridCommand;
        public ICommand RefreshGridCommand => refreshGridCommand ??= new AsyncRelayCommand(RefreshGridAsync);

        /// <summary>
        /// Rechnet das Raster mit <see cref="TestPhase"/> durch, damit man die Punkte einer
        /// späteren Phase schon im Aufbau sieht.
        /// <para>
        /// <b>Über <c>SetPhase</c>, nicht über das Feld:</b> eine bereits gespielte Zelle behält
        /// ihre eingefrorenen Punkte. Direkt gesetzt umging das den Riegel und schrieb auch
        /// gespielten Zellen neue Punkte.
        /// </para>
        /// </summary>
        private async Task RefreshGridAsync(object? commandParameter)
        {
            if (Game == null) return;

            foreach (var cell in Game.GameGridCoordinates)
            {
                cell.SetPhase(TestPhase);
            }

            await RebuildCellsAsync();
        }
    }
}
