using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// P11-2 (Durchsicht M37, P12): das echte Fragefenster schließt mit Server und Telefonen. Vorher legte sein XAML ein zweites
    /// <see cref="CurrentQuestionViewModel"/> an; das hing zuerst an <c>Closed</c> und räumte über <c>StaticManager</c> die
    /// prozessweite Buzzer-Steuerung ab - die Telefone sahen die nächste Runde, bevor die Bewertung gebucht war. Eine Sperre auf
    /// Berts Ergebniszeile hält die Buchung auf, bis sie nachweislich wartet.
    /// </summary>
    [TestClass]
    public class FragefensterSchliessenUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.FragefensterSchliessen;

        private LocalBuzzer.Service.Base.LayoutStateManager Hub => serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        [TestMethod]
        public async Task ThePhonesSeeTheNextRoundOnlyAfterTheBooking()
        {
            var vm = await OeffneAsync(CellSpecialRule.None);
            vm.SchliessenBucht = true;
            Assert.AreEqual(BuzzerControlsLayout.Buzzer, vm.CurrentBuzzerLayout, "Die Frage liegt nicht als Buzzer auf den Telefonen.");

            await phones[1].InvokeAsync("Buzz");
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Berts Buzzer öffnet das Bewertungsfenster nicht.");
            Karte(vm, 1).RightSuggestionCommand.Execute(null);

            object? zweites = null;
            var fenster = UiTestHost.Dispatcher.Invoke(() =>
            {
                // Schmal: unter 100 Bildpunkten speichert WindowBase beim Schließen keine Lage in die Ablage des Nutzers.
                var f = new QuestionMasterView { Width = 50, Height = 50 };
                f.DataContextChanged += (_, e) => zweites ??= e.OldValue;
                f.DataContext = vm;
                return f;
            });

            var resetsVorher = Hub.ResetCount;
            int resetsVorBuchung, wartende;
            string? siegerInDerLuecke;

            await using (var sperre = new Quizzer.Logic.Context.DataContext())
            {
                await using var tx = await sperre.Database.BeginTransactionAsync();
                await sperre.Database.ExecuteSqlRawAsync(
                    "SELECT Id FROM [question].[QuestionResult] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE Id = {0}", Karte(vm, 1).Result.Id);
                var sitzung = await sperre.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS Value").SingleAsync();

                await UiTestHost.Dispatcher.InvokeAsync(fenster.Close);
                wartende = await WartendeAsync(sitzung);
                await Task.Delay(500); // was die Telefone bis hier bekommen, kam vor der Buchung

                resetsVorBuchung = Hub.ResetCount - resetsVorher;

                await phones[2].InvokeAsync("Buzz"); // Clara buzzert in der Lücke
                await Task.Delay(300);
                siegerInDerLuecke = Hub.BuzzerState.Winner?.Designation;

                await tx.RollbackAsync();
            }

            await vm.ClosedTask;
            Assert.IsTrue(await WarteAsync(() => Hub.ResetCount > resetsVorher), "Nach dem Schließen wurde die Runde nie zurückgesetzt.");
            await Task.Delay(300);

            var bert = await BertAsync();
            Console.WriteLine($"P11-2: zweites ViewModel {zweites?.GetType().Name ?? "-"}, Buchung wartete {wartende}, Resets vor der Buchung {resetsVorBuchung}, "
                + $"insgesamt {Hub.ResetCount - resetsVorher}, Layout {Stand(0)?.Layout}, Sieger in der Lücke {siegerInDerLuecke ?? "-"}, "
                + $"Bert RightCount {bert.RightCount}, Punkte {bert.FinalScore}");

            TestEnvironment.ThrowIfAnythingWasSwallowed();
            Assert.IsTrue(wartende > 0, "Die Sperre hielt die Buchung nie auf - dann misst der Test nichts.");
            Assert.AreEqual(1, bert.RightCount, "Berts Bewertung wurde nicht genau einmal gebucht.");
            Assert.AreEqual(0, resetsVorBuchung, "Die Telefone sahen die nächste Runde, bevor die Bewertung gebucht war.");
            Assert.AreEqual(1, Hub.ResetCount - resetsVorher, "Das Schließen setzte die Runde nicht genau einmal zurück.");
            Assert.IsNull(zweites, $"Das Fragefenster legt selbst ein {zweites?.GetType().Name} an - es hängt an Loaded und Closed mit.");
        }

        /// <summary>Wie viele Anfragen an der Sperre der <paramref name="sitzung"/> hängen - wartet bis zu 5 s auf eine.</summary>
        private static async Task<int> WartendeAsync(int sitzung)
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var wartende = 0;

            while (uhr.Elapsed < TimeSpan.FromSeconds(5) && wartende == 0)
            {
                await using var blick = new Quizzer.Logic.Context.DataContext();
                wartende = await blick.Database
                    .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE blocking_session_id = {sitzung}")
                    .SingleAsync();

                await Task.Delay(20);
            }

            return wartende;
        }

        private async Task<QuestionResult> BertAsync()
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world!.Coordinate.Id) ?? [];
            return zeilen.Single(r => r.PlayerId == world.Players[1].Id);
        }
    }
}
