using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Alles, was der Spieleditor in die Datenbank schreibt.
    /// <para>
    /// Eigene Teildatei, weil <c>EditGameViewModel.cs</c> ueber der Groessen-Obergrenze liegt
    /// und durch eine Aenderung nicht laenger werden darf (standards-allgemein.md §5). Der
    /// Weg ist derselbe wie bei <c>CurrentQuestionViewModel</c>: Teildatei nach Thema.
    /// </para>
    /// </summary>
    public partial class EditGameViewModel
    {
        /// <summary>
        /// Führt einen Schreibvorgang unter derselben Sperre aus wie den Rasteraufbau.
        /// <para>
        /// <c>RequestGridRebuildAsync</c> läuft entkoppelt und mit 200 ms Verzögerung; es legt
        /// Zellen an und löscht welche. Wer in diesem Fenster gleichzeitig speichert, schreibt
        /// auf dieselben Zeilen. Genau diese Bauart - ein losgeschickter Schreibvorgang neben
        /// einem zweiten - hat am 2026-09-06 im Spielleiter-Fenster dazu geführt, dass sich ein
        /// Spiel nicht mehr öffnen ließ. Hier ist es dieselbe Lage, und die Sperre gab es schon;
        /// sie wurde nur von einer Seite genommen.
        /// </para>
        /// </summary>
        private async Task RunGuardedAsync(Func<Task> arbeit)
        {
            await rebuildGridLock.WaitAsync();

            try
            {
                await arbeit();
            }
            finally
            {
                rebuildGridLock.Release();
            }
        }

        /// <summary>
        /// Schreibt eine einzelne Zelle samt ihrer Punkte.
        /// <para>
        /// Die Punkte sind gespeicherte Spalten. Bis 2026-09-06 blieb die Zuweisung einer Frage
        /// nur im Speicher stehen: die Kachel zeigte sofort "600 / −165 Punkte", in der Datenbank
        /// stand weiterhin die Null, und wer das Fenster ohne "Speichern" schloss, spielte die
        /// Frage spaeter fuer null Punkte. In der Spieldatenbank stehen deshalb fuenf von sechs
        /// belegten Zellen eines Spiels auf null.
        /// </para>
        /// </summary>
        private async Task SaveCoordinateAsync(GameGridCoordinate? coordinate)
        {
            if (coordinate == null || Game == null)
                return;

            await RunGuardedAsync(async () =>
            {
                coordinate.Game = Game;
                coordinate.CalculateAndSetCurrentPoints();

                using var ctrlCoords = new GameGridCoordinatesController();

                await ctrlCoords.UpsertAsync(coordinate);
                await ctrlCoords.SaveChangesAsync();
            });
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        /// <summary>
        /// Schreibt Spiel, Kopfzeilen und Zellen in <b>einem</b> Vorgang.
        /// <para>
        /// Bis 2026-09-06 waren es drei getrennte: scheiterte der dritte, war der erste schon
        /// festgeschrieben, und zurueck kam ein Spiel mit neuer Bezeichnung, aber altem Raster.
        /// Der verkettende Konstruktor <c>ControllerBase(other)</c> teilt den DataContext, eine
        /// einzige Sicherung schreibt alles. Gemessen in
        /// <c>SharedContextTransactionUnitTests</c> - samt der Gegenrichtung, dass es ohne
        /// Verkettung wirklich auseinanderfaellt.
        /// </para>
        /// <para>
        /// Die Zellpunkte werden vorher neu gerechnet: wer die Faktoren aendert und speichert,
        /// erwartet, dass die neuen Werte in der Datenbank stehen. Gespielte Zellen bleiben
        /// unberuehrt, darum kuemmert sich <c>CalculateAndSetCurrentPoints</c> selbst.
        /// </para>
        /// </summary>
        public override async Task VMSaveAsync()
        {
            if (Game == null) return;

            await RunGuardedAsync(async () =>
            {
                Game.CalculateAndSetCurrentPoints();

                using var ctrlGames = new GamesController();
                using var ctrlHeader = new HeadersController(ctrlGames);
                using var ctrlCells = new GameGridCoordinatesController(ctrlGames);

                await ctrlGames.UpsertAsync(Game);
                await ctrlHeader.UpsertAsync(Game.Headers);
                await ctrlCells.UpsertAsync(Game.GameGridCoordinates);

                await ctrlGames.SaveChangesAsync();
            });
        }

        private AsyncRelayCommand? resetGameBuildCommand;

        public ICommand ResetGameBuildCommand =>
            resetGameBuildCommand ??= new AsyncRelayCommand(ResetGameBuildAsync, _ => IsBuilding);

        /// <summary>
        /// Räumt den Spielaufbau leer - Ergebnisse, Zellen, Zuordnungen, Kopfzeilen.
        /// <para>
        /// <b>B26.</b> Bis hierher lief das als einziger Schreibweg des Spieleditors <b>ohne</b>
        /// die Sperre, die <c>RequestGridRebuildAsync</c> und beide Speicherwege nehmen. Der Fall galt
        /// als unerreichbar, weil die Rückfrage den 200-ms-Anlauf des Rasteraufbaus längst
        /// überdauert - genau so eine Begründung hielt aber schon einmal, bis ein Spiel sich
        /// nicht mehr öffnen ließ.
        /// </para>
        /// <para>
        /// <b>Gefragt wird vor der Sperre, nicht darin.</b> Ein modales Fenster hinter einem
        /// genommenen Riegel hielte den Rasteraufbau so lange auf, wie der Spielleiter zum Lesen
        /// braucht.
        /// </para>
        /// </summary>
        private async Task ResetGameBuildAsync(object? commandParameter)
        {
            if (Game == null)
                return;

            if (!UserPrompt.Confirm(
                    "Spielaufbau wirklich zurücksetzen? Damit werden alle zugewiesenen Fragen "
                    + "und Spieler aus dem Spiel entfernt.",
                    "Zurücksetzen bestätigen"))
            {
                return;
            }

            await RunGuardedAsync(async () =>
            {
                Game.State = GameState.Building;

                using var ctrlGame = new GamesController();
                await ctrlGame.SaveChangesAsync();

                using var ctrlErgebnisse = new QuestionResultsController(ctrlGame);
                await ctrlErgebnisse.DeleteByGameIdAsync(Game.Id);

                using var ctrlZellen = new GameGridCoordinatesController(ctrlGame);
                await ctrlZellen.DeleteByGameIdAsync(Game.Id);

                using var ctrlZuordnungen = new PlayerXGamesController(ctrlGame);
                await ctrlZuordnungen.DeleteByGameIdAsync(Game.Id);

                using var ctrlKopfzeilen = new HeadersController(ctrlGame);
                await ctrlKopfzeilen.DeleteByGameIdAsync(Game.Id);
            });

            // Das Neuladen baut das Raster neu auf und SCHREIBT dabei (GridBuilder legt
            // Kopfzeilen und Zellen an) - es gehoert deshalb ebenfalls unter die Sperre.
            // Ein zweiter Aufruf statt eines erweiterten: SemaphoreSlim ist nicht
            // wiedereintrittsfaehig, ein Guard im Guard verklemmte sich selbst.
            await RunGuardedAsync(() => LoadModel(Game.Id));
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }
    }
}
