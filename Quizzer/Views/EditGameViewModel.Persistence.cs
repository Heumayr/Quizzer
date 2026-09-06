using Quizzer.Base;
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

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }
    }
}
