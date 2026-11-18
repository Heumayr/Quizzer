using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// P10-6/P11-1 (Durchsicht M37): die Spielleitung schließt ein Fenster, während „Speichern und Zelle abschließen“ im
    /// Bewertungsfenster noch speichert. Eine Zeilensperre auf Berts Ergebniszeile hält das Speichern auf, bis es nachweislich
    /// an ihr wartet; geschlossen wird über den echten Weg wie in <see cref="SchliessenVorWeiterrechnenUnitTests"/>. Danach ist
    /// Bert genau einmal gebucht, die Zelle erledigt, bevor das Spielfenster weiterrechnet, und der Abschlussschritt kam vor dem
    /// Räumen des Beamers.
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

        /// <summary>P11-1: das Fragefenster schließt, während das Abschließen speichert - die Zelle blieb unerledigt.</summary>
        [TestMethod]
        public async Task ClosingTheQuestionWhileTheFinishIsSavedWaitsAndBooksOnce()
        {
            var lauf = await SpieleMitSperreAsync(ergebnisfensterZuerst: false);

            Assert.IsFalse(lauf.FertigUnterSperre, "Das Spielfenster war fertig, während das Speichern noch wartete.");
            PruefeAbschluss(lauf);
        }

        /// <summary>
        /// P11-1, zweiter Weg derselben Ursache: die Spielleitung schließt das Bewertungsfenster selbst, während es speichert. Sein
        /// <c>Closed</c>-Haken las „abgeschlossen“, bevor es feststand; das Fragefenster bleibt hier offen, bis gespeichert ist.
        /// </summary>
        [TestMethod]
        public async Task ClosingTheResultWindowWhileTheFinishIsSavedStillFinishesTheCell()
        {
            var lauf = await SpieleMitSperreAsync(ergebnisfensterZuerst: true);

            PruefeAbschluss(lauf);
        }

        private void PruefeAbschluss(Lauf lauf)
        {
            Console.WriteLine($"P11-1 Bert: RightCount {lauf.Bert.RightCount}, Punkte {lauf.Bert.FinalScore}, Zelle erledigt {lauf.ErledigtAlsSpielfensterFertig}, "
                + $"Verlauf {string.Join(" > ", lauf.Verlauf)}");

            var verschluckt = TestEnvironment.SwallowedExceptions.ToList();
            Assert.AreEqual(0, verschluckt.Count, "Beim Schließen scheiterte ein zweites Speichern: " + string.Join(" | ", verschluckt.Select(e => e.Message)));
            Assert.AreEqual(1, lauf.Bert.RightCount, "Berts Bewertung wurde nicht genau einmal gebucht.");
            Assert.AreEqual(lauf.Zellpunkte, lauf.Bert.FinalScore, "Berts Punkte stehen nicht genau einmal da.");
            Assert.IsTrue(lauf.ErledigtAlsSpielfensterFertig, "Als das Spielfenster weiterrechnete, war die Zelle nicht erledigt.");

            var abschluss = lauf.Verlauf.LastIndexOf(Abschluss);
            Assert.IsTrue(abschluss >= 0, "Der Abschlussschritt kam nie auf den Beamer.");
            Assert.IsTrue(lauf.Verlauf.LastIndexOf(Raster) > abschluss, "Der Beamer wurde vor dem Abschlussschritt geräumt - danach stand die Auflösung über dem Raster.");
        }

        private const string Abschluss = "Abschluss";
        private const string Raster = "Raster";

        private sealed record Lauf(QuestionResult Bert, int Zellpunkte, bool FertigUnterSperre, bool ErledigtAlsSpielfensterFertig, List<string> Verlauf);

        /// <summary>
        /// Bewertet Bert „richtig“ und startet „Speichern und Zelle abschließen“ gegen eine Sperre auf seiner Zeile. Dann schließt
        /// das Fragefenster - oder mit <paramref name="ergebnisfensterZuerst"/> zuerst das Bewertungsfenster und das Fragefenster
        /// erst, wenn die Sperre weg ist. Der Verlauf schreibt mit, wann der Abschlussschritt kam und wann das Raster.
        /// </summary>
        private async Task<Lauf> SpieleMitSperreAsync(bool ergebnisfensterZuerst)
        {
            var leiter = await OeffneSpielAsync();
            var zelle = leiter.GameGridVMs.CellVMs.Single(c => c.Coordinate.Id == world.Coordinate.Id);
            var verlauf = new List<string>();
            Exception? fehler = null;
            Task speichern = Task.CompletedTask;
            bool speichernWartete = false;
            bool fertigUnterSperre;
            var freigegeben = new TaskCompletionSource();

            leiter.GamePlayerViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GamePlayerViewModel.ShowGameGridView) && leiter.GamePlayerViewModel.ShowGameGridView == Visibility.Visible)
                    verlauf.Add(Raster);
            };

            await using (var sperre = new Quizzer.Logic.Context.DataContext())
            {
                Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
                var gesperrt = new TaskCompletionSource();

                // Gesperrt wird erst im Fenster: die Ergebniszeilen legt das Laden der Frage an. Und über den Schlüssel - eine
                // Sperre über die Zellen-Spalte liegt nur im Nebenindex, und das Speichern ginge an ihr vorbei.
                leiter.FragefensterBauen = frage => SchliessenVorWeiterrechnenUnitTests.Fragefenster(frage, async karten =>
                {
                    frage.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(CurrentQuestionViewModel.CurrentStepContext) && frage.IsDone)
                            verlauf.Add(Abschluss);
                    };

                    (tx, var sitzung) = await SperreBertAsync(sperre, karten);
                    speichern = ((AsyncRelayCommand)frage.PlayersResultViewModel!.SaveIsDoneFinishStateCommand).ExecuteAsync(null);
                    speichernWartete = await WartendeAsync(sitzung, mindestens: 1) >= 1;

                    if (ergebnisfensterZuerst)
                        frage.PlayersResultViewModel.Window!.Close();

                    gesperrt.TrySetResult();

                    if (ergebnisfensterZuerst)
                        await freigegeben.Task; // das Fragefenster schließt erst nach dem Speichern
                }, ex => { fehler = ex; gesperrt.TrySetResult(); });

                var klick = UiTestHost.Dispatcher.InvokeAsync(() => ((AsyncRelayCommand)leiter.CellClickCommand).ExecuteAsync(zelle)).Task.Unwrap();

                await Task.WhenAny(gesperrt.Task, Task.Delay(30000));
                await Task.WhenAny(klick, Task.Delay(1500)); // ein Spielfenster, das nicht wartet, ist spätestens jetzt fertig
                fertigUnterSperre = klick.IsCompleted;

                if (tx != null)
                    await tx.RollbackAsync();

                await speichern;
                freigegeben.TrySetResult();
                await klick;
            }

            var erledigt = await ZelleErledigtAsync();

            Assert.IsNull(fehler, "Das Fragefenster lief nicht durch: " + fehler);
            Assert.IsTrue(speichernWartete, "Die Sperre hielt das Speichern nie auf - dann misst der Test nichts.");

            return new Lauf(await BertAsync(), zelle.Coordinate.CurrentPoints, fertigUnterSperre, erledigt, verlauf);
        }

        /// <summary>Bewertet Bert „richtig“ und sperrt seine Ergebniszeile über den Schlüssel; gibt die Sitzung der Sperre zurück.</summary>
        private async Task<(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction, int)> SperreBertAsync(
            Quizzer.Logic.Context.DataContext sperre, List<PlayerResultContext> karten)
        {
            var bert = karten.Single(k => k.Player.Id == world.Players[1].Id);

            var tx = await sperre.Database.BeginTransactionAsync();
            await sperre.Database.ExecuteSqlRawAsync(
                "SELECT Id FROM [question].[QuestionResult] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE Id = {0}", bert.Result.Id);
            var sitzung = await sperre.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS Value").SingleAsync();

            bert.RightSuggestionCommand.Execute(null);
            return (tx, sitzung);
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
