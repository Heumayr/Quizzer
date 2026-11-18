using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Runde zurücksetzen“ an einer Teamregel und im Finale (V22, R5-2), echter Server und Telefone, Rot = Plätze 0-1, Blau = 2-3:
    /// der Knopf schickt das Layout, das auf den Telefonen liegt - wie die Bindung es sieht, nicht wie der Getter -, und nach dem
    /// Zurücksetzen steht vor der nächsten Abgabe nichts Altes mehr im Fragefenster: keine Antwort, keine Auflösung am Beamer,
    /// kein Vorschlag auf einer Karte, und die Karten und der Stand sagen wieder, was sie vor der ersten Abgabe sagten.
    /// </summary>
    [TestClass]
    public class RundeZuruecksetzenUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.RundeZuruecksetzen;

        /// <summary>Was die Bindung am Knopf hält: gelesen, bevor das Fenster lädt, danach nur bei <c>PropertyChanged</c>.</summary>
        private BuzzerControlsLayout gebunden;

        /// <summary>Stand und Kartentexte, bevor jemand abgegeben hat - so soll es nach dem Zurücksetzen wieder aussehen.</summary>
        private (string Stand, string[] Karten) anfang;

        private async Task<TestableCurrentQuestionViewModel> OeffneAsync(CellSpecialRule regel, QuestionType typ = QuestionType.Default)
        {
            FensterEinrichten = vm =>
            {
                gebunden = vm.CurrentBuzzerLayout;
                vm.PropertyChanged += (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(vm.CurrentBuzzerLayout))
                        gebunden = vm.CurrentBuzzerLayout;
                };
                return Task.CompletedTask;
            };

            return await OeffneTeamregelAsync(regel, typ: typ);
        }

        private void MerkeAnfang(TestableCurrentQuestionViewModel vm)
            => anfang = (vm.Sonderregel.Stand, vm.PlayersResultViewModel!.PlayerResultContextList.Select(k => k.SonderregelText).ToArray());

        /// <summary>„Runde zurücksetzen“ mit dem Parameter, den die Bindung hält - die Telefone müssen danach dasselbe Layout zeigen.</summary>
        private async Task ZuruecksetzenAsync(TestableCurrentQuestionViewModel vm, BuzzerControlsLayout erwartet)
        {
            var vorher = Hub.ResetCount;
            await ((AsyncRelayCommand)vm.BuzzerControlsViewModel!.ResetRoundCommand).ExecuteAsync(gebunden);

            Assert.IsTrue(Hub.ResetCount > vorher, "Die Runde wurde nicht zurückgesetzt.");
            Assert.AreEqual(erwartet, Hub.CurrentLayout, "„Runde zurücksetzen“ schickt ein anderes Layout, als auf den Telefonen lag.");
        }

        /// <summary>Alles, was nach dem Zurücksetzen noch von der alten Runde im Fragefenster steht - leer, wenn nichts.</summary>
        private string Altes(TestableCurrentQuestionViewModel vm)
        {
            var altes = new List<string>();
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;

            for (var i = 0; i < karten.Count; i++)
            {
                var karte = karten[i];
                var wer = karte.Player.CalculatedDisplayName;

                if (karte.InputResult != null)
                    altes.Add($"{wer}: Antwort {karte.InputResult.Value}");
                if (karte.SelectionResult != null)
                    altes.Add($"{wer}: Tasten {string.Join(",", karte.SelectionResult.SelectedKeys)}");
                if (karte.Suggestion != ScoreSuggestion.None || karte.CurrentScoreManipulation != 0)
                    altes.Add($"{wer}: {karte.SuggestionText}");
                if (karte.SonderregelText != anfang.Karten[i])
                    altes.Add($"{wer}: Karte sagt {karte.SonderregelText}");
            }

            if (vm.Sonderregel.Aufloesung.Count > 0)
                altes.Add("Beamer: " + string.Join(" / ", vm.Sonderregel.Aufloesung));
            if (vm.Sonderregel.Stand != anfang.Stand)
                altes.Add("Stand: " + vm.Sonderregel.Stand);
            if (vm.GleichGedachtGruppen.Count > 0)
                altes.Add($"{vm.GleichGedachtGruppen.Count} Gruppen zum Anhaken");
            if (vm.TeamAbstimmungGleichstaende.Count > 0)
                altes.Add($"{vm.TeamAbstimmungGleichstaende.Count} Gleichstände zum Entscheiden");
            if (vm.FinaleAufloesenVisibility == Visibility.Visible)
                altes.Add("Finale: Nächsten auflösen");

            return string.Join(" | ", altes);
        }

        /// <summary>Die Zusicherung über den Verlauf: nach dem Zurücksetzen, vor der nächsten Abgabe.</summary>
        private void KeinAltes(TestableCurrentQuestionViewModel vm)
        {
            var altes = Altes(vm);
            Assert.IsTrue(altes.Length == 0, "Nach dem Zurücksetzen steht noch Altes im Fragefenster: " + altes);
        }

        private async Task StimmeAsync(TestableCurrentQuestionViewModel vm, int telefon, string taste)
        {
            await phones[telefon].InvokeAsync("SelectionResults", new { selectedKeys = new[] { taste } });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, telefon).SelectionResult?.SelectedKeys.SequenceEqual([taste]) == true),
                $"Die Stimme von Telefon {telefon} kam nicht auf die Karte.");
        }

        /// <summary>R5-2: ohne Vorphase steht die Teamregel erst beim Laden - der Knopf muss es erfahren, sonst schickt er „Buzzer“.</summary>
        [TestMethod]
        public async Task TheButtonSendsTheLayoutOnThePhonesAndTheSpeakerAnswersAgain()
        {
            var vm = await OeffneAsync(CellSpecialRule.TeamCouncil);
            Assert.AreEqual(BuzzerControlsLayout.Buzzer, vm.Question!.BuzzerControlsLayout, "Die Testfrage ist keine Buzzerfrage.");
            Assert.AreEqual(BuzzerControlsLayout.Input, gebunden, "Die Bindung am Knopf erfährt nicht, dass der Sprecher tippt.");
            MerkeAnfang(vm);

            await SchreibeAsync(vm, 0, "Paris");
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.IsTrue(Karte(vm, 1).CurrentScoreManipulation > 0, "Die Teamberatung verteilt nichts - der Test misst nichts.");

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.Input);
            KeinAltes(vm);

            await SchreibeAsync(vm, 0, "Rom");
            Assert.IsNull(Karte(vm, 2).InputResult, "Der Sprecher von Blau hat eine Antwort, ohne neu abzugeben.");

            // Gegenrichtung: das Schließen setzt die Telefone auch zurück - die neue Bewertung bleibt auf der Karte.
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            await vm.ClosedForTestAsync();
            Assert.AreEqual(ScoreSuggestion.Right, Karte(vm, 0).Suggestion, "Das Schließen der Frage nimmt die Bewertung von der Karte.");
            Assert.IsTrue(Karte(vm, 1).CurrentScoreManipulation > 0, "Das Schließen der Frage nimmt den Anteil von der Karte.");
        }

        [TestMethod]
        public async Task GleichGedachtForgetsTheComparison()
        {
            var vm = await OeffneAsync(CellSpecialRule.SameThought);
            MerkeAnfang(vm);

            foreach (var (telefon, text) in new[] { (0, "Paris"), (1, "Paris"), (2, "Rom"), (3, "Rom") })
                await SchreibeAsync(vm, telefon, text);
            Assert.IsTrue(await WarteAsync(() => vm.GleichGedachtGruppen.Count > 0), "Nach der letzten Antwort wird nicht verglichen.");

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.Input);
            KeinAltes(vm);

            // Neu schreiben nur drei - die alte Antwort von Platz 3 zählt nicht mehr mit.
            await SchreibeAsync(vm, 0, "Wien");
            await SchreibeAsync(vm, 1, "Wien");
            await SchreibeAsync(vm, 2, "Rom");
            await ((AsyncRelayCommand)vm.BuzzerControlsViewModel!.CloseRoundCommand).ExecuteAsync(null);
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Aufloesung.Count == 2), "Nach dem Schließen wird nicht verglichen.");
            Assert.AreEqual(0, Karte(vm, 2).CurrentScoreManipulation, "Blau hat mit der alten Antwort von Platz 3 gleich gedacht.");
            Assert.IsTrue(Karte(vm, 0).CurrentScoreManipulation > 0, "Rot bekommt für die neue Übereinstimmung nichts.");
        }

        [TestMethod]
        public async Task AlleOderKeinerForgetsAnswersAndRatings()
        {
            var vm = await OeffneAsync(CellSpecialRule.AllOrNothing);
            MerkeAnfang(vm);

            await SchreibeAsync(vm, 0, "Paris");
            await SchreibeAsync(vm, 1, "Paris");
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Karte(vm, 1).Suggestion = ScoreSuggestion.Right;
            Assert.IsTrue(Karte(vm, 0).CurrentScoreManipulation > 0, "Alle oder keiner verteilt nichts - der Test misst nichts.");

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.Input);
            KeinAltes(vm);
        }

        [TestMethod]
        public async Task TeamAbstimmungForgetsTheVote()
        {
            var vm = await OeffneAsync(CellSpecialRule.TeamVote, QuestionType.MultipleChoice);
            MerkeAnfang(vm);

            await StimmeAsync(vm, 0, "A");
            await StimmeAsync(vm, 1, "B");
            await StimmeAsync(vm, 2, "B");
            await StimmeAsync(vm, 3, "B");
            Assert.IsTrue(await WarteAsync(() => vm.Sonderregel.Aufloesung.Count == 2), "Nach der letzten Stimme wird nicht gezählt.");

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.KeySelect);
            KeinAltes(vm);
        }

        [TestMethod]
        public async Task TheFinalForgetsAnswersAndWhatWasRevealed()
        {
            var vm = await OeffneAsync(CellSpecialRule.Final);
            Assert.IsTrue(vm.VorphaseLaeuft, "Das Finale setzt nicht ein.");

            for (var i = 0; i < 4; i++)
                await AbgebenAsync(i, CellSpecialRule.Final, new { amount = 10 });
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Nach allen Einsätzen ist die Vorphase nicht zu.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input && Hub.AnswerLock.Active), "Die Sprecher antworten nicht.");
            MerkeAnfang(vm);

            await SchreibeAsync(vm, 0, "Ottawa");
            await SchreibeAsync(vm, 2, "Toronto");
            Assert.IsTrue(await WarteAsync(() => vm.FinaleAufloesenVisibility == Visibility.Visible), "Nach beiden Sprechern lässt sich nicht auflösen.");
            Assert.IsTrue(vm.LoeseFinaleWeiterAuf());
            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;

            await ZuruecksetzenAsync(vm, BuzzerControlsLayout.Input);
            KeinAltes(vm);
            Assert.IsTrue(Hub.BuzzerInputState.EinmalAbgabe, "Nach dem Zurücksetzen darf der Sprecher die Antwort ändern.");

            // Der Sprecher von Rot gibt neu ab - aufgelöst wird erst, wenn auch Blau wieder abgegeben hat.
            await SchreibeAsync(vm, 0, "Montreal");
            Assert.AreEqual(Visibility.Collapsed, vm.FinaleAufloesenVisibility, "Mit der alten Antwort von Blau lässt sich schon auflösen.");
        }
    }
}
