using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.TestShared;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Allianz im Fragefenster (M35 C3b), echter Server, echte Telefone: nur der Ausloeser waehlt, nur das Paar antwortet,
    /// Plus nur wenn beide richtig sind. Ohne Ausloeser keine Vorphase und keine Allianz.
    /// </summary>
    [TestClass]
    public class SonderregelAllianzUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.SonderregelAllianz;

        [TestMethod]
        public async Task OnlyTheTriggerChoosesOnlyThePairAnswersAndPlusNeedsBoth()
        {
            ZelleEinrichten = _ => world!.Game.CurrentChoosingPlayerId = world.Players[0].Id;
            var vm = await OeffneAsync(CellSpecialRule.Alliance, spieler: 3);
            var ids = world!.Players.Select(p => p.Id).ToList();
            var feld = world.Coordinate.CurrentPoints;
            var steuerung = serverVm!._server!.BuzzerController!;

            Assert.IsTrue(vm.VorphaseLaeuft, "Mit Ausloeser spielt die Allianz keine Vorphase.");
            Assert.IsTrue(Zustand.IsLockedFor(world.Players[1]), "Bert darf in der Vorphase abgeben, obwohl nur Anna waehlt.");

            // Bert waehlt trotzdem (WLAN): verworfen, die Vorphase bleibt offen.
            await AbgebenAsync(1, CellSpecialRule.Alliance, new { chosen = ids[2] });
            await Task.Delay(200);
            Assert.IsTrue(vm.VorphaseLaeuft, "Die Wahl eines Zuschauers hat die Vorphase geschlossen.");

            await AbgebenAsync(0, CellSpecialRule.Alliance, new { chosen = ids[1] });
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Nach Annas Wahl ist die Vorphase nicht zu.");

            Assert.IsTrue(await WarteAsync(() => steuerung.StateManager.AnswerLock.Answerers?.OrderBy(i => i).SequenceEqual(new[] { ids[0], ids[1] }.OrderBy(i => i)) == true),
                "Am Server antwortet nicht genau das Paar.");
            Assert.IsTrue(await WarteAsync(() => Stand(2)?.ExcludedFromQuestion == true), "Claras Telefon ist nicht gesperrt.");
            StringAssert.Contains(Karte(vm, 0).SonderregelText, world.Players[1].CalculatedDisplayName);

            // --- Nur Anna richtig: kein Plus; dann beide: volle Punkte ---
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Nur einer richtig - trotzdem ein Plus.");
            Karte(vm, 1).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, Karte(vm, 0).CurrentScoreManipulation, "Beide richtig - Annas Karte wechselt nicht.");
            Assert.AreEqual(feld, Karte(vm, 1).CurrentScoreManipulation);

            // --- Bert falsch: sein Minus bleibt, Annas Plus faellt ---
            Karte(vm, 1).Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-world.Coordinate.CurrentMinusPoints, Karte(vm, 1).CurrentScoreManipulation, "Das Minus des Partners ist veraendert.");
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Annas Plus bleibt, obwohl Bert falsch ist.");
        }

        [TestMethod]
        public async Task WithoutATriggerThereIsNoAlliance()
        {
            ZelleEinrichten = _ => world!.Game.CurrentChoosingPlayerId = Guid.Empty;
            var vm = await OeffneAsync(CellSpecialRule.Alliance, spieler: 2);

            Assert.IsFalse(vm.VorphaseLaeuft, "Ohne Ausloeser spielt die Allianz eine Vorphase.");
            Assert.AreNotEqual(MinigamePhase.Running, Zustand.Phase);
            Assert.IsFalse(serverVm!._server!.BuzzerController!.StateManager.AnswerLock.Active, "Ohne Allianz ist jemand gesperrt.");
            StringAssert.Contains(vm.Sonderregel.Hinweis, "Keine Allianz");

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(world!.Coordinate.CurrentPoints, Karte(vm, 0).CurrentScoreManipulation, "Ohne Allianz zaehlt richtig nicht.");
        }
    }
}
