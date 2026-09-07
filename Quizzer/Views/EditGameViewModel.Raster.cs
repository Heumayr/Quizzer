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

        /// <summary>
        /// Wie viele Phasen der Abend haben soll.
        /// <para>
        /// <b>Bis 2026-09-07 gab es dafür kein Feld.</b> Jedes neue Spiel bekam drei Phasen und
        /// behielt sie; wer zwei oder gar keine Steigerung wollte, kam nur über die Datenbank
        /// heran. Dabei entscheidet die Zahl, <b>wie oft</b> das Spiel mitten im Abend nach dem
        /// Phasenwechsel fragt und damit die Punkte hochsetzt: <c>CalculatetThreshold</c> teilt
        /// die belegten Zellen in so viele gleich große Blöcke.
        /// </para>
        /// <para>
        /// <b>Unter 1 wird auf 1 gehoben</b> - dieselbe Vorsorge wie bei
        /// <see cref="TestPhase"/>. Eine 0 hieße „keine Schwellen", was harmlos aussieht, aber
        /// über <c>SuggestedPhases &lt;= 0</c> auch die Anzeige „Phase 1 von 0" ergäbe.
        /// </para>
        /// </summary>
        public int SuggestedPhases
        {
            get => Game?.SuggestedPhases ?? 0;
            set
            {
                if (Game == null) return;

                var gewollt = value < 1 ? 1 : value;

                if (Game.SuggestedPhases == gewollt) return;

                Game.SuggestedPhases = gewollt;
                OnPropertyChanged();
            }
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
