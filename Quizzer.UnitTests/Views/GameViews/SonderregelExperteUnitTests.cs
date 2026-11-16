using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Experte im Fragefenster (M35 C3b), echter Server, echte Telefone: niemand waehlt sich selbst, bei Gleichstand
    /// antwortet der erste in der Spielerreihenfolge, der Spielleiter waehlt um - die Sperre am Server folgt -, und
    /// nur die Waehler des richtigen Experten bekommen den Anteil.
    /// </summary>
    [TestClass]
    public class SonderregelExperteUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => 5427;

        private async Task WaehleAsync(int telefon, Guid gewaehlt)
            => await AbgebenAsync(telefon, CellSpecialRule.Expert, new { chosen = gewaehlt });

        [TestMethod]
        public async Task OnATieTheGameMasterPicksTheExpertAndOnlyHisVotersGain()
        {
            ZelleEinrichten = z => z.SpecialRuleShare = 20;
            var vm = await OeffneAsync(CellSpecialRule.Expert, spieler: 3);
            var ids = world!.Players.Select(p => p.Id).ToList();
            var feld = world.Coordinate.CurrentPoints;
            var steuerung = serverVm!._server!.BuzzerController!;

            // Sich selbst waehlen geht nicht - die Abgabe zaehlt nicht.
            await WaehleAsync(1, ids[1]);
            await WaehleAsync(0, ids[1]);
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Stand.Contains("1 von")), "Die erste Wahl kam nicht an: " + vm.Sonderregel.Stand);
            Assert.IsTrue(vm.VorphaseLaeuft);

            // Jeder eine Stimme: Gleichstand, Vorgabe ist Anna (erste in der Reihenfolge).
            await WaehleAsync(1, ids[2]);
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Stand.Contains("2 von")), "Die zweite Wahl kam nicht an: " + vm.Sonderregel.Stand);
            await WaehleAsync(2, ids[0]);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Nach der letzten Wahl ist die Vorphase nicht zu.");

            // Die Spielerreihenfolge ist die der Karten.
            var reihenfolge = vm.PlayersResultViewModel!.PlayerResultContextList.Select(k => k.Player.Id).ToList();
            Assert.IsTrue(await WarteAsync(() => steuerung.StateManager.AnswerLock.Answerers?.SequenceEqual([reihenfolge[0]]) == true),
                "Bei Gleichstand antwortet nicht der erste in der Spielerreihenfolge.");
            Assert.AreEqual(3, vm.SonderregelKandidaten.Count, "Der Spielleiter sieht nicht alle Gleichauf.");
            Assert.AreEqual(Visibility.Visible, vm.SonderregelKandidatVisibility);

            // --- Der Spielleiter waehlt den zweiten: Sperre, Telefon und Beamer folgen ---
            var experte = ids.IndexOf(reihenfolge[1]);
            var waehler = (experte + 2) % 3;           // 0 waehlte 1, 1 waehlte 2, 2 waehlte 0
            var dritter = 3 - experte - waehler;
            await vm.WaehleKandidatAsync(ids[experte]);
            Assert.IsTrue(await WarteAsync(() => steuerung.StateManager.AnswerLock.Answerers?.SequenceEqual([ids[experte]]) == true),
                "Die Sperre am Server folgt der Wahl des Spielleiters nicht.");
            Assert.IsTrue(await WarteAsync(() => Stand(waehler)?.ExcludedFromQuestion == true), "Nach dem Umwaehlen ist ein anderes Telefon nicht gesperrt.");
            StringAssert.Contains(vm.Sonderregel.Hinweis, world.Players[experte].CalculatedDisplayName, "Der Beamer nennt den neuen Experten nicht.");
            Assert.AreEqual(ids[experte], vm.GewaehlterKandidat?.Id);

            // --- Der Experte richtig: nur wer ihn waehlte, bekommt den Anteil ---
            var bonus = Punkteanteil.Von(feld, 20);
            Karte(vm, experte).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(bonus, Karte(vm, waehler).CurrentScoreManipulation, "Die Karte des Waehlers bekommt den Anteil nicht.");
            Assert.AreEqual(0, Karte(vm, dritter).CurrentScoreManipulation, "Wer einen anderen waehlte, bekommt etwas.");
            Assert.AreEqual(feld, Karte(vm, experte).CurrentScoreManipulation, "Der Experte bekommt nicht die Feldpunkte.");

            Karte(vm, experte).Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(0, Karte(vm, waehler).CurrentScoreManipulation, "Ein falscher Experte bringt seinen Waehlern etwas.");
        }
    }
}
