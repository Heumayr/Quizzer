using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// P10-6 (Durchsicht M37, P11): die Spielleitung schließt das Fragefenster, während „Speichern und Zelle abschließen“ im
    /// Bewertungsfenster noch speichert. Eine Zeilensperre auf Berts Ergebniszeile hält das erste Speichern auf, bis es
    /// nachweislich an ihr wartet; dann schließt das Fragefenster - über den echten Weg wie in
    /// <see cref="SchliessenVorWeiterrechnenUnitTests"/>. Das Schließen wartet auf das laufende Speichern, statt ein zweites zu
    /// starten (das scheiterte an der RowVersion), und das Spielfenster mit ihm.
    /// </summary>
    [TestClass]
    public class AbschlussBeimSchliessenUnitTests
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

        [TestMethod]
        public async Task ClosingTheQuestionWhileTheFinishIsSavedWaitsAndBooksOnce()
        {
            var leiter = await OeffneSpielAsync();
            var zelle = leiter.GameGridVMs.CellVMs.Single(c => c.Coordinate.Id == world.Coordinate.Id);
            Exception? fehler = null;
            Task speichern = Task.CompletedTask;
            bool speichernWartete = false;
            bool fertigUnterSperre;

            await using (var sperre = new Quizzer.Logic.Context.DataContext())
            {
                Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
                var sitzung = 0;
                var gesperrt = new TaskCompletionSource();

                // Gesperrt wird erst im Fenster: die Ergebniszeilen legt das Laden der Frage an. Und über den Schlüssel - eine
                // Sperre über die Zellen-Spalte liegt nur im Nebenindex, und das Speichern ginge an ihr vorbei.
                leiter.FragefensterBauen = frage => SchliessenVorWeiterrechnenUnitTests.Fragefenster(frage, async karten =>
                {
                    var bert = karten.Single(k => k.Player.Id == world.Players[1].Id);

                    tx = await sperre.Database.BeginTransactionAsync();
                    await sperre.Database.ExecuteSqlRawAsync(
                        "SELECT Id FROM [question].[QuestionResult] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE Id = {0}", bert.Result.Id);
                    sitzung = await sperre.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS Value").SingleAsync();

                    bert.RightSuggestionCommand.Execute(null);
                    speichern = ((AsyncRelayCommand)frage.PlayersResultViewModel!.SaveIsDoneFinishStateCommand).ExecuteAsync(null);
                    speichernWartete = await WartendeAsync(sitzung, mindestens: 1) >= 1; // dann schließt das Fragefenster
                    gesperrt.TrySetResult();
                }, ex => { fehler = ex; gesperrt.TrySetResult(); });

                var klick = UiTestHost.Dispatcher.InvokeAsync(() => ((AsyncRelayCommand)leiter.CellClickCommand).ExecuteAsync(zelle)).Task.Unwrap();

                await Task.WhenAny(gesperrt.Task, Task.Delay(30000));
                await Task.WhenAny(klick, Task.Delay(1500)); // ein Spielfenster, das nicht wartet, ist spätestens jetzt fertig
                fertigUnterSperre = klick.IsCompleted;

                if (tx != null)
                    await tx.RollbackAsync();

                await klick;
                await speichern;
            }

            Assert.IsNull(fehler, "Das Fragefenster lief nicht durch: " + fehler);
            Assert.IsTrue(speichernWartete, "Die Sperre hielt das Speichern nie auf - dann misst der Test nichts.");

            var bert = await BertAsync();
            var verschluckt = TestEnvironment.SwallowedExceptions.ToList();

            // Ob die Zelle danach erledigt ist, steht nur in der Ausgabe: das ist ein eigener Befund (P11-1), hier nicht behoben.
            Console.WriteLine($"P10-6 Bert: RightCount {bert.RightCount}, Punkte {bert.FinalScore}, Spielfenster fertig unter Sperre {fertigUnterSperre}, "
                + $"Zelle erledigt {await ZelleErledigtAsync()}, verschluckt {verschluckt.Count}");

            Assert.AreEqual(0, verschluckt.Count, "Beim Schließen scheiterte ein zweites Speichern: " + string.Join(" | ", verschluckt.Select(e => e.Message)));
            Assert.AreEqual(1, bert.RightCount, "Berts Bewertung wurde nicht genau einmal gebucht.");
            Assert.AreEqual(zelle.Coordinate.CurrentPoints, bert.FinalScore, "Berts Punkte stehen nicht genau einmal da.");
            Assert.IsFalse(fertigUnterSperre, "Das Spielfenster war fertig, während das Speichern noch wartete.");
        }

        /// <summary>Wie viele Anfragen an der Sperre der <paramref name="sitzung"/> hängen - wartet bis zu 5 s auf <paramref name="mindestens"/>.</summary>
        private static async Task<int> WartendeAsync(int sitzung, int mindestens)
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var wartende = 0;

            while (uhr.Elapsed < TimeSpan.FromSeconds(5))
            {
                await using var blick = new Quizzer.Logic.Context.DataContext();
                wartende = await blick.Database
                    .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE blocking_session_id = {sitzung}")
                    .SingleAsync();

                if (wartende >= mindestens)
                    break;

                await Task.Delay(20);
            }

            return wartende;
        }

        private async Task<QuestionResult> BertAsync()
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? [];
            return zeilen.Single(r => r.PlayerId == world.Players[1].Id);
        }

        private async Task<bool> ZelleErledigtAsync()
        {
            await using var db = new Quizzer.Logic.Context.DataContext();
            return await db.Set<GameGridCoordinate>().AsNoTracking().Where(c => c.Id == world.Coordinate.Id).Select(c => c.IsDone).SingleAsync();
        }

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
    }
}
