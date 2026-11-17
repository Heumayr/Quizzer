using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Sonderregeln;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Das Finale ohne Teams im Fragefenster (M37 E4), echter Server und Telefone: jeder setzt verdeckt, dann antwortet jeder
    /// genau einmal per Text. Aufgelöst wird erst, wenn die Antworten zu sind - einer nach dem anderen, aufsteigend nach Stand,
    /// mit Einsatz und Antwort am Beamer. Jede Bewertung ist ein Vorschlag auf der Karte: der eigene Einsatz, ohne einen 0,
    /// gekappt (E2). Das Finale im Teamspiel steht in <c>FinaleTeamUnitTests</c>.
    /// </summary>
    [TestClass]
    public class FinaleUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.Finale;

        private LayoutStateManager Hub => serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        private string Name(int spieler) => world!.Players[spieler].CalculatedDisplayName;

        /// <summary>Anna 300, Bert 30, Carl 500; Anna setzt 200, Bert 100 (seine Grenze sind die Feldpunkte), Carl nichts.</summary>
        private async Task<TestableCurrentQuestionViewModel> NachDenEinsaetzenAsync()
        {
            var vm = await OeffneAsync(CellSpecialRule.Final, spieler: 3, kontostaende: [300, 30, 500]);

            Assert.IsTrue(vm.VorphaseLaeuft, "Das Finale spielt keine Vorphase.");
            Assert.IsInstanceOfType<StakeRules>(Zustand.Rules, "Die Vorphase des Finales ist kein Einsatz.");
            Assert.AreEqual("Sonderregel: Finale", vm.Sonderregel.Titel);

            await AbgebenAsync(0, CellSpecialRule.Final, new { amount = 200 });
            await AbgebenAsync(1, CellSpecialRule.Final, new { amount = 100 });
            Assert.IsTrue(await WarteAsync(() => Zustand.Rules?.CurrentSubmissions.Count == 2), "Die Einsätze kamen nicht an.");

            await vm.SchliesseVorphaseAsync();
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Vorphase lässt sich nicht schließen.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input), "Nach den Einsätzen kommt keine Eingabe.");
            return vm;
        }

        [TestMethod]
        public async Task EveryoneAnswersOnceByTextAndTheRevealWaitsForTheAnswers()
        {
            var vm = await NachDenEinsaetzenAsync();

            Assert.AreEqual(CurrentQuestionViewModel.FinaleEingabe, Hub.BuzzerInputState.Infos.Placeholder, "Die Telefone bekommen keine Texteingabe.");
            Assert.AreEqual("text", Hub.BuzzerInputState.Infos.InputType);
            Assert.IsTrue(Hub.BuzzerInputState.EinmalAbgabe, "Im Finale darf man die Antwort ändern.");
            Assert.IsFalse(Hub.AnswerLock.Active, "Ohne Teams antworten nicht alle selbst.");
            Assert.AreEqual("Einsatz: 200", Karte(vm, 0).SonderregelText);
            Assert.AreEqual("Einsatz: 100", Karte(vm, 1).SonderregelText);
            Assert.AreEqual("Kein Einsatz – setzt 0", Karte(vm, 2).SonderregelText);

            await phones[0].InvokeAsync("SubmitInput", new { value = "Ottawa" });
            await phones[1].InvokeAsync("SubmitInput", new { value = "Toronto" });
            await phones[0].InvokeAsync("SubmitInput", new { value = "Toronto" });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, 1).InputResult?.Value == "Toronto"), "Berts Antwort kam nicht auf seine Karte.");
            await Task.Delay(200);
            Assert.AreEqual("Ottawa", Karte(vm, 0).InputResult?.Value, "Annas zweite Antwort hat die erste ersetzt.");

            // Noch fehlt Carl: nichts aufzulösen, und der Beamer zeigt weder Einsatz noch Antwort.
            Assert.AreEqual(Visibility.Collapsed, vm.FinaleAufloesenVisibility, "Auflösen geht, bevor alle geantwortet haben.");
            Assert.IsFalse(vm.LoeseFinaleWeiterAuf(), "Aufgelöst, bevor alle geantwortet haben.");
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count);
            Assert.AreEqual(Visibility.Collapsed, vm.Sonderregel.AufloesungVisibility);

            await phones[2].InvokeAsync("SubmitInput", new { value = "Ottawa" });
            Assert.IsTrue(await WarteAsync(() => vm.FinaleAufloesenVisibility == Visibility.Visible), "Nach der letzten Antwort lässt sich nicht auflösen.");
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count, "Der Beamer zeigt schon vor dem ersten Aufdecken etwas.");
            StringAssert.Contains(vm.Sonderregel.Stand, $"als Nächstes {Name(1)}", "Der Spielleiter sieht nicht, wer als Nächstes kommt.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Einer nach dem anderen, vom niedrigsten Stand zum höchsten, je mit Stand, Einsatz und Antwort.</summary>
        [TestMethod]
        public async Task TheBeamerRevealsOneByOneFromTheLowestBalance()
        {
            var vm = await NachDenEinsaetzenAsync();
            await phones[0].InvokeAsync("SubmitInput", new { value = "Ottawa" });
            await phones[1].InvokeAsync("SubmitInput", new { value = "Toronto" });

            // Carl antwortet nicht: der Spielleiter schließt die Runde, dann geht es los.
            Assert.IsTrue(await WarteAsync(() => Karte(vm, 1).InputResult != null));
            await serverVm!.BuzzerControlsViewModel!.CloseRoundAsync(null);
            Assert.IsTrue(await WarteAsync(() => vm.FinaleAufloesenVisibility == Visibility.Visible), "Nach „Runde schließen“ lässt sich nicht auflösen.");

            Assert.IsTrue(vm.LoeseFinaleWeiterAuf());
            CollectionAssert.AreEqual(new[] { $"{Name(1)} – Stand 30 · Einsatz 100 · „Toronto“" }, vm.Sonderregel.Aufloesung.ToArray());
            Assert.AreEqual(Visibility.Visible, vm.Sonderregel.AufloesungVisibility, "Der Beamer zeigt die Auflösung nicht.");

            Assert.IsTrue(vm.LoeseFinaleWeiterAuf());
            Assert.IsTrue(vm.LoeseFinaleWeiterAuf());
            CollectionAssert.AreEqual(new[]
            {
                $"{Name(1)} – Stand 30 · Einsatz 100 · „Toronto“",
                $"{Name(0)} – Stand 300 · Einsatz 200 · „Ottawa“",
                $"{Name(2)} – Stand 500 · Einsatz 0 · keine Antwort",
            }, vm.Sonderregel.Aufloesung.ToArray());

            Assert.IsFalse(vm.LoeseFinaleWeiterAuf(), "Es lässt sich mehr auflösen, als es gibt.");
            Assert.AreEqual(Visibility.Collapsed, vm.FinaleAufloesenVisibility);
            StringAssert.Contains(vm.Sonderregel.Stand, "Alle 3 aufgelöst");

            // Die Frage ist zu: der Beamer räumt die Auflösung weg.
            await vm.ClosedForTestAsync();
            Assert.AreEqual(0, vm.Sonderregel.Aufloesung.Count, "Nach der Frage bleibt die Auflösung stehen.");
        }

        /// <summary>E1: jede Bewertung schlägt den eigenen Einsatz vor - ohne Einsatz 0, gekappt nach E2.</summary>
        [TestMethod]
        public async Task EachRatingProposesTheOwnStakeOnTheCard()
        {
            var vm = await NachDenEinsaetzenAsync();
            var anna = Karte(vm, 0);
            var bert = Karte(vm, 1);
            var carl = Karte(vm, 2);

            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(200, anna.CurrentScoreManipulation, "Richtig bringt nicht den Einsatz.");
            anna.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-200, anna.CurrentScoreManipulation, "Falsch kostet nicht den Einsatz.");
            StringAssert.Contains(anna.SuggestionText, "Einsatz 200");

            carl.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(0, carl.CurrentScoreManipulation, "Ohne Einsatz zählen im Finale die Feldpunkte.");

            // E2: Bert setzt 100 bei 30 auf dem Konto - ohne Minus im Spiel verliert er höchstens die 30.
            bert.Coordinate!.Game.AllowNegativeScore = false;
            bert.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-30, bert.CurrentScoreManipulation, "Der verlorene Einsatz ist nicht gekappt.");
        }
    }
}
