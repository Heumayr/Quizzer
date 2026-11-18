using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// P10-1 (Durchsicht M37, P11): das Häkchen „Richtig beantwortet“ allein, ohne Bewertung, überlebt das Schließen des
    /// Fragefensters - über den echten Schließweg (<c>OnCellClickedAsync</c>, <c>ShowDialog</c>, das echte Bewertungsfenster).
    /// Es ist eine offene Bewertung, aber keine Buchung: Punkte und Zähler bleiben, wie sie waren.
    /// </summary>
    [TestClass]
    public class HaekchenBeimSchliessenUnitTests
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
        public async Task TheTickAloneIsStoredWhenTheQuestionCloses()
        {
            await SpieleZelleAsync(await OeffneSpielAsync(), karte => karte.CorrectAnswered = true);

            var zeile = await BertAsync();
            Assert.IsTrue(zeile.CorrectAnswered, "Das Häkchen ging beim Schließen verloren.");
            Assert.AreEqual((0, 0, 0, 0), (zeile.RightCount, zeile.WrongCount, zeile.CorrectionsCount, zeile.FinalScore),
                "Mit dem Häkchen wurden Punkte oder Zähler gebucht.");
        }

        /// <summary>Die Gegenrichtung: ein gespeichertes Häkchen, beim zweiten Öffnen entfernt, ist nach dem Schließen weg.</summary>
        [TestMethod]
        public async Task RemovingAStoredTickIsStoredWhenTheQuestionCloses()
        {
            var leiter = await OeffneSpielAsync();
            await SpieleZelleAsync(leiter, karte => karte.CorrectAnswered = true);
            Assert.IsTrue((await BertAsync()).CorrectAnswered, "Das Häkchen stand nach dem ersten Schließen nicht da - dann misst der Test nichts.");

            await SpieleZelleAsync(leiter, karte => karte.CorrectAnswered = false);

            Assert.IsFalse((await BertAsync()).CorrectAnswered, "Das entfernte Häkchen kam beim Schließen nicht in der Datenbank an.");
        }

        /// <summary>Nach „Speichern“ ist das Häkchen nicht mehr offen - sonst schriebe das Schließen es ein zweites Mal.</summary>
        [TestMethod]
        public async Task AStoredTickIsNoLongerOpen()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            var ergebnisse = vm.PlayersResultViewModel!;
            var karte = ergebnisse.PlayerResultContextList.Single(k => k.Player.Id == world.Players[1].Id);

            Assert.IsFalse(ergebnisse.HatOffeneBewertungen, "Schon beim Öffnen offen - dann misst der Test nichts.");
            karte.CorrectAnswered = true;
            Assert.IsTrue(ergebnisse.HatOffeneBewertungen, "Das Häkchen allein gilt nicht als offene Bewertung.");
            Assert.IsFalse(ergebnisse.HatGebucht, "Das Häkchen allein gilt als Buchung.");

            Assert.IsTrue(await ergebnisse.TrySaveAsync(), "Speichern scheiterte.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.IsFalse(ergebnisse.HatOffeneBewertungen, "Nach dem Speichern gilt das Häkchen weiter als offen.");
        }

        /// <summary>Spielt die Zelle über den echten Schließweg und lässt <paramref name="setze"/> Berts Karte ändern.</summary>
        private async Task SpieleZelleAsync(GameMasterViewModel leiter, Action<PlayerResultContext> setze)
        {
            var zelle = leiter.GameGridVMs.CellVMs.Single(c => c.Coordinate.Id == world.Coordinate.Id);
            Exception? fehler = null;

            leiter.FragefensterBauen = frage => SchliessenVorWeiterrechnenUnitTests.Fragefenster(frage, karten =>
            {
                setze(karten.Single(k => k.Player.Id == world.Players[1].Id));
                return Task.CompletedTask;
            }, ex => fehler = ex);

            await UiTestHost.Dispatcher.InvokeAsync(() => ((AsyncRelayCommand)leiter.CellClickCommand).ExecuteAsync(zelle)).Task.Unwrap();

            Assert.IsNull(fehler, "Das Fragefenster lief nicht durch: " + fehler);
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        private async Task<QuestionResult> BertAsync()
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? [];
            return zeilen.Single(r => r.PlayerId == world.Players[1].Id);
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
