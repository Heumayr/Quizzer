using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// R5-3 (Durchsicht M37): das Spielfenster rechnet erst weiter, wenn die beim Schließen gespeicherte Bewertung gebucht ist.
    /// Gemessen über den <b>echten</b> Schließweg - <c>OnCellClickedAsync</c>, <c>ShowDialog</c>, das echte Bewertungsfenster,
    /// <c>ViewModelBase.Window_Closed</c> -, nicht über <c>ClosedForTestAsync</c>; nur das Fragefenster ist ein schlichtes
    /// <see cref="Window"/> statt der <c>QuestionMasterView</c>.
    /// </summary>
    [TestClass]
    public class SchliessenVorWeiterrechnenUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 3);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>
        /// Nur Bert ist bewertet (falsch) - seine Karte bucht das Schließen erst nach dem ersten <c>await</c>. Davor sah
        /// <c>HatSichGeaendert</c> nichts, und die Rücknahme blieb bei der vorigen Zelle („Noch nichts gewertet").
        /// </summary>
        [TestMethod]
        public async Task TheGameWindowWaitsForTheRatingBookedOnClose()
        {
            var leiter = await OeffneSpielAsync();
            var zelle = leiter.GameGridVMs.CellVMs.Single(c => c.Coordinate.Id == world.Coordinate.Id);
            Exception? fehler = null;

            leiter.FragefensterBauen = frage => Fragefenster(frage, karten =>
            {
                karten.Single(k => k.Player.Id == world.Players[1].Id).WrongSuggestionCommand.Execute(null);
                return Task.CompletedTask;
            }, ex => fehler = ex);

            await UiTestHost.Dispatcher.InvokeAsync(() => ((AsyncRelayCommand)leiter.CellClickCommand).ExecuteAsync(zelle)).Task.Unwrap();

            var gebuchtAlsEsWeiterging = await BertGebuchtAsync();

            Assert.IsNull(fehler, "Das Fragefenster lief nicht durch: " + fehler);
            var spaeterGebucht = await WarteAsync(BertGebuchtAsync);
            TestEnvironment.ThrowIfAnythingWasSwallowed();
            Assert.IsTrue(spaeterGebucht, "Die Bewertung wird gar nicht gebucht - dann misst der Test nichts.");

            Assert.IsTrue(leiter.UndoCommand.CanExecute(null),
                "Nach dem Schließen bietet die Rücknahme die gespielte Zelle nicht an - das Spielfenster rechnete vor der Buchung weiter.");
            StringAssert.Contains(leiter.UndoToolTip, world.Coordinate.QuestionBase!.Designation);
            Assert.IsTrue(gebuchtAlsEsWeiterging, "Als das Spielfenster fertig war, stand Berts Bewertung noch nicht in der Datenbank.");
        }

        /// <summary>
        /// P3-2: „Speichern und Zelle abschließen“ schließt das Bewertungsfenster, und dessen <c>Closed</c>-Haken speichert die Zelle
        /// als erledigt - als <c>async void</c>. Schließt das Fragefenster gleich danach, darf das Spielfenster erst weiterrechnen,
        /// wenn die Zelle gespeichert ist. Eine Zeilensperre hält das Speichern 1,5 s auf - ohne sie gewinnt es das Rennen fast
        /// immer, und die Zusicherung wäre auch ohne das Warten grün.
        /// </summary>
        [TestMethod]
        public async Task TheGameWindowWaitsForTheCellSavedAsDoneOnFinish()
        {
            var leiter = await OeffneSpielAsync();
            var zelle = leiter.GameGridVMs.CellVMs.Single(c => c.Coordinate.Id == world.Coordinate.Id);
            Exception? fehler = null;

            leiter.FragefensterBauen = frage => Fragefenster(frage, async karten =>
            {
                karten.Single(k => k.Player.Id == world.Players[1].Id).RightSuggestionCommand.Execute(null);
                await ((AsyncRelayCommand)frage.PlayersResultViewModel!.SaveIsDoneFinishStateCommand).ExecuteAsync(null);
            }, ex => fehler = ex);

            bool speichernWarteteNoch;

            await using (var sperre = new Quizzer.Logic.Context.DataContext())
            {
                var tx = await sperre.Database.BeginTransactionAsync();
                await sperre.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM [base].[GameGridCoordinate] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE Id = {0}", world.Coordinate.Id);
                var frei = Task.Run(async () => { await Task.Delay(1500); await tx.RollbackAsync(); });

                await UiTestHost.Dispatcher.InvokeAsync(() => ((AsyncRelayCommand)leiter.CellClickCommand).ExecuteAsync(zelle)).Task.Unwrap();
                speichernWarteteNoch = !frei.IsCompleted;
                await frei;
            }

            Assert.IsNull(fehler, "Das Fragefenster lief nicht durch: " + fehler);
            var spaeterErledigt = await WarteAsync(ZelleErledigtAsync);
            TestEnvironment.ThrowIfAnythingWasSwallowed();
            Assert.IsTrue(spaeterErledigt, "Die Zelle wird gar nicht als erledigt gespeichert - dann misst der Test nichts.");
            Assert.IsFalse(speichernWarteteNoch, "Das Spielfenster war fertig, während das Speichern der erledigten Zelle noch wartete.");
        }

        private async Task<bool> ZelleErledigtAsync()
        {
            using var ctrl = new GameGridCoordinatesController();
            return (await ctrl.GetAsync(world.Coordinate.Id))?.IsDone == true;
        }

        private async Task<bool> BertGebuchtAsync()
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? [];
            return zeilen.SingleOrDefault(r => r.PlayerId == world.Players[1].Id)?.WrongCount == 1;
        }

        private static async Task<bool> WarteAsync(Func<Task<bool>> bedingung)
        {
            for (var i = 0; i < 100; i++)
            {
                if (await bedingung())
                    return true;

                await Task.Delay(50);
            }

            return false;
        }

        /// <summary>Öffnet das Spiel wie <c>WaehlerMarkierungUnitTests</c>: Spielleiter am Spiel, LoadModel und Laden des Fensters.</summary>
        private async Task<GameMasterViewModel> OeffneSpielAsync()
        {
            world.Game.ModeratorPlayerId = world.Players[0].Id;
            using (var ctrl = new GamesController())
            {
                await ctrl.UpdateAsync(world.Game);
                await ctrl.SaveChangesAsync();
            }

            var leiter = new GameMasterViewModel();
            Assert.IsNotNull(await leiter.LoadModel(world.Game.Id), "Das Spiel liess sich nicht öffnen: " + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
            await leiter.LoadForTestAsync();
            return leiter;
        }

        /// <summary>
        /// Ein schlichtes Fenster an Stelle der <c>QuestionMasterView</c>: lädt die Frage, öffnet das echte Bewertungsfenster,
        /// lässt <paramref name="bewerten"/> die Karten setzen und schließt - alles im modalen Rahmen von <c>ShowDialog</c>.
        /// Erst nach dem Laden hängt es am ViewModel, damit <c>Loaded</c> nicht ein zweites Mal lädt; geschlossen wird über
        /// <c>ViewModelBase.Window_Closed</c>. Eine Notbremse schließt nach 30 s, damit kein Lauf am Dialog hängen bleibt.
        /// </summary>
        internal static Window Fragefenster(CurrentQuestionViewModel frage, Func<List<PlayerResultContext>, Task> bewerten, Action<Exception> fehler)
        {
            var fenster = new Window { Width = 200, Height = 100, Left = -2000, Top = -2000, ShowInTaskbar = false, ShowActivated = false };
            var notbremse = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };

            notbremse.Tick += (_, _) =>
            {
                notbremse.Stop();
                fehler(new TimeoutException("Das Fragefenster schloss nicht von selbst."));
                fenster.Close();
            };

            fenster.Loaded += async (_, _) =>
            {
                notbremse.Start();

                try
                {
                    await frage.LoadForTestAsync();
                    frage.Window = fenster;

                    await ((AsyncRelayCommand)frage.OpenResultsCommand).ExecuteAsync(null);
                    await bewerten(frage.PlayersResultViewModel!.PlayerResultContextList);
                }
                catch (Exception ex)
                {
                    fehler(ex);
                }

                notbremse.Stop();
                fenster.Close();
            };

            return fenster;
        }
    }
}
