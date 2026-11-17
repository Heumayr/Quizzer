namespace Quizzer.Views.GameViews
{
    /// <summary>Wie weit das Spiel ist: Kopfzeile des Spielleiterfensters und das Spielende.</summary>
    public partial class GameMasterViewModel
    {
        /// <summary>
        /// Ob alle spielbaren Zellen abgeschlossen sind.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> hier stand <c>GameGridCoordinates.All(...)</c> - und der
        /// Rasteraufbau legt für <i>jede</i> Position eine Zeile an, auch die leeren. In der
        /// Spieldatenbank hatte „Test Spiel" 25 Zellen, davon <b>19 ohne Frage</b>. Das Spiel
        /// konnte damit nie fertig werden: keine Siegerehrung, kein Phasenwechsel, und die
        /// Leiste meldete bis zuletzt „Noch 19 offen".
        /// </para>
        /// </summary>
        public bool IsGameFinished =>
            Game != null && Game.SpielbareZellen.Any() && Game.SpielbareZellen.All(c => c.IsDone);

        public int CurrentRound
        {
            get => Game?.CurrentRound ?? 0;
            set
            {
                Game?.CurrentRound = value;
                OnPropertyChanged();
            }
        }

        public int GameGridCoordinatesCount => Game?.SpielbareZellen.Count() ?? 0;
        public int GameGridCoordinatesDoneCount => Game?.SpielbareZellen.Count(c => c.IsDone) ?? 0;

        /// <summary>
        /// Wie weit das Spiel ist, in einem Satz. Bis 2026-09-06 standen hier zwei Angaben
        /// nebeneinander - "Round" und "Done" -, die dieselbe Zahl mit Versatz eins zeigten.
        /// </summary>
        public string ProgressHeadline =>
            GameGridCoordinatesCount == 0
                ? "Kein Spielfeld"
                : $"Frage {Math.Min(GameGridCoordinatesDoneCount + 1, GameGridCoordinatesCount)} von {GameGridCoordinatesCount}";

        public string OpenCellsText
        {
            get
            {
                var offen = GameGridCoordinatesCount - GameGridCoordinatesDoneCount;

                return offen switch
                {
                    <= 0 => "Alle Fragen gespielt",
                    1 => "Noch eine offen",
                    _ => $"Noch {offen} offen",
                };
            }
        }
    }
}
