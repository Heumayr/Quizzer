using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Spielablauf;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;
using Quizzer.Views.GameViews;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Der Sprecherstand (M37 T5): nach einer bewerteten Teamregel-Zelle spricht in jedem Team der Nächste - geschrieben und an
    /// die Telefone -, ansehen und schließen bewegt nichts, „Sprecher weiter/zurück“ geht von Hand, und „Beim Start
    /// zurücksetzen“ setzt ihn auf den Anfang.
    /// </summary>
    public partial class TeamberatungUnitTests
    {
        private static async Task<int> StandInDerDatenbankAsync(Guid team)
        {
            using var ctrl = new GameTeamsController();
            return (await ctrl.GetAsync(team))!.SpeakerTurn;
        }

        [TestMethod]
        public async Task ARatedTeamRuleCellMovesEverySpeakerOnAndThePhonesFollow()
        {
            var vm = await OeffneTeamberatungAsync();
            var leiter = new GameMasterViewModel { Game = world!.Game };
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Speichern scheiterte.");
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel.Results;
            await vm.ClosedForTestAsync();

            Assert.IsTrue(vm.TeamregelGespielt);
            await leiter.NachTeamregelAsync(vm, merk);

            Assert.AreEqual((1, 1), (await StandInDerDatenbankAsync(rot.Id), await StandInDerDatenbankAsync(blau.Id)), "Der Sprecherstand steht nicht in der Datenbank.");
            Assert.IsTrue(await WarteAsync(() => Stand(0)?.Team?.Speaker == Name(1)), "Annas Telefon nennt nicht den neuen Sprecher von Rot.");
            Assert.IsTrue(await WarteAsync(() => Stand(2)?.Team?.Speaker == Name(3)), "Das Telefon von Blau nennt nicht den neuen Sprecher.");
            StringAssert.Contains(leiter.SprecherText, $"Rot – {Name(1)}");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Wer die Zelle nur ansieht und schließt, bewegt den Sprecher nicht - wie die Rücknahme (Zellwahl).</summary>
        [TestMethod]
        public async Task LookingAtTheCellMovesNoSpeaker()
        {
            var vm = await OeffneTeamberatungAsync();
            var leiter = new GameMasterViewModel { Game = world!.Game };
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);

            await vm.ClosedForTestAsync();
            await leiter.NachTeamregelAsync(vm, merk);

            Assert.AreEqual(0, await StandInDerDatenbankAsync(rot.Id), "Ansehen und schließen bewegte den Sprecher.");
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.Team?.Speaker == Name(0)), "Nach dem Schließen nennt das Telefon nicht wieder den Sprecher des Stands.");
        }

        /// <summary>Eine gewöhnliche Zelle im Teamspiel bewegt den Sprecher nicht - nur eine Teamregel.</summary>
        [TestMethod]
        public async Task AnOrdinaryCellMovesNoSpeaker()
        {
            var vm = await OeffneTeamberatungAsync(regel: CellSpecialRule.None);
            var leiter = new GameMasterViewModel { Game = world!.Game };
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync());
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel.Results;
            await leiter.NachTeamregelAsync(vm, merk);

            Assert.IsFalse(vm.TeamregelGespielt);
            Assert.AreEqual(0, await StandInDerDatenbankAsync(rot.Id));
        }

        [TestMethod]
        public async Task TheMasterMovesTheSpeakerForwardAndBackByHand()
        {
            await OeffneTeamberatungAsync();
            var leiter = new GameMasterViewModel { Game = world!.Game };

            Assert.IsTrue(leiter.SprecherZurueckCommand.CanExecute(null));
            await ((AsyncRelayCommand)leiter.SprecherZurueckCommand).ExecuteAsync(null);

            Assert.AreEqual(1, await StandInDerDatenbankAsync(rot.Id), "„Sprecher zurück“ vom Anfang trifft nicht den Letzten.");
            Assert.IsTrue(await WarteAsync(() => Stand(0)?.Team?.Speaker == Name(1)));

            await ((AsyncRelayCommand)leiter.SprecherWeiterCommand).ExecuteAsync(null);
            Assert.IsTrue(await WarteAsync(() => Stand(0)?.Team?.Speaker == Name(0)), "Nach „Sprecher weiter“ nennt das Telefon den alten.");

            await ((AsyncRelayCommand)leiter.SprecherWeiterCommand).ExecuteAsync(null);
            Assert.AreEqual((3, 3), (await StandInDerDatenbankAsync(rot.Id), await StandInDerDatenbankAsync(blau.Id)));
            Assert.IsTrue(await WarteAsync(() => Stand(0)?.Team?.Speaker == Name(1)), "Beim Stand 3 spricht in Rot nicht der Zweite.");
        }

        /// <summary>„Beim Start zurücksetzen“ (seit T2): der Sprecherstand ist Verlauf und geht mit den Ergebnissen.</summary>
        [TestMethod]
        public async Task ResettingTheResultsAtTheStartResetsTheSpeakers()
        {
            await OeffneTeamberatungAsync();
            var leiter = new GameMasterViewModel { Game = world!.Game };
            await leiter.SprecherBewegenAsync(vor: true);
            Assert.AreEqual(1, await StandInDerDatenbankAsync(rot.Id), "Die Vorbedingung fehlt - der Stand ging nicht hoch.");

            Assert.IsTrue(await EditGameViewModel.ResetGameResultsAsync(world.Game), "Das Zurücksetzen wurde nicht bestätigt.");

            Assert.AreEqual((0, 0), (await StandInDerDatenbankAsync(rot.Id), await StandInDerDatenbankAsync(blau.Id)), "Der Sprecherstand überlebt das Zurücksetzen.");
        }
    }
}
