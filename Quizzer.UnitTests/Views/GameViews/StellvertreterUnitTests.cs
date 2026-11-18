using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Stellvertreter-Duell im Fragefenster (M37 T10), echter Server und Telefone; Rot = Plätze 0 und 1, Blau = 2 und 3. Am Beamer
    /// stehen erst die Teams und wen sie schicken, dann die Runde der Stellvertreter; das Team des Siegers bekommt die Feldpunkte
    /// aufgeteilt als Vorschlag (E1, F-M37-06). Wählt ein Team nicht, lost der Spielleiter.
    /// </summary>
    [TestClass]
    public class StellvertreterUnitTests : TeamregelServerTestBasis
    {
        protected override int Port => TestPorts.Stellvertreter;

        private Task<TestableCurrentQuestionViewModel> OeffneAsync(int variante = StellvertreterEvaluator.ReaktionVariant, int runden = 1)
            => OeffneTeamMinispielAsync(MinigameKind.ProxyDuel, f =>
            {
                f.Variant = variante;
                f.Rounds = runden;
            });

        private Task GibAsync(int telefon, object nutzlast)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.ProxyDuel,
                durchgang = Zustand.Durchgang,
                payload = nutzlast,
            });

        /// <summary>Der Platz, der für Blau spielt - beim Los entscheidet der Server.</summary>
        private int BlauerVertreter()
        {
            var id = Zustand.Lenke(r => ((ProxyDuelRules)r!).Vertreter).Single(v => v.TeamId == blau.Id).SpielerId;
            return id == Id(2) ? 2 : 3;
        }

        [TestMethod]
        public async Task TheTeamsChooseOnTheBeamerAndTheWinnersTeamSplitsTheFieldValue()
        {
            var vm = await OeffneAsync();
            var feld = PunkteJetzt(world!.Coordinate, vm);

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "die Teams wählen");
            Assert.AreEqual(2, vm.Reaktion.Zeilen.Count, "Am Beamer stehen nicht beide Teams.");
            Assert.IsTrue(vm.Reaktion.Zeilen.All(z => z.Zeit == "wählt …"), "Ein Team hat schon einen Stellvertreter.");
            Assert.AreEqual(Visibility.Visible, vm.StellvertreterLosenVisibility, "Während der Wahl fehlt der Knopf zum Auslosen.");

            await GibAsync(0, new { chosen = Id(1), lot = false });
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == "✚ Rot" && z.Zeit == Name(1))),
                "Der Beamer zeigt nicht, wen Rot schickt.");

            await GibAsync(3, new { lot = true });
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Runde 1 von 1")), "Nach der letzten Wahl beginnt das Duell nicht: " + vm.Reaktion.Kopfzeile);
            var blauer = BlauerVertreter();
            StringAssert.Contains(vm.Reaktion.Zeilen.Single(z => z.Name == "★ Blau").Zeit, "(gelost)");
            Assert.IsTrue(await WarteAsync(() => vm.StellvertreterLosenVisibility == Visibility.Collapsed), "Nach der Wahl bleibt der Knopf zum Auslosen.");
            Assert.IsTrue(await WarteAsync(() => serverVm!.BuzzerControlsViewModel!.LivePlayers.Any(p => p.AnswerText == "hat gelost")),
                "Die Übersicht des Spielleiters zeigt das Los nicht.");

            // Platz 0 ist kein Stellvertreter - seine Zeit zählt nicht; danach die beiden.
            await GibAsync(0, new { falseStart = false, milliseconds = 120 });
            await GibAsync(1, new { falseStart = false, milliseconds = 200 });
            await GibAsync(blauer, new { falseStart = false, milliseconds = 300 });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Das Duell endet nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual($"Ergebnis – {Name(1)} gewinnt für ✚ Rot", vm.Reaktion.Kopfzeile);
            Assert.AreEqual(("★ Sieger", "200 ms"), (vm.Reaktion.Zeilen[0].SiegerText, vm.Reaktion.Zeilen[0].Zeit));

            Assert.AreEqual(feld, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation, "Rot bekommt nicht genau den Feldwert.");
            Assert.AreEqual((ScoreSuggestion.Correction, ScoreSuggestion.Correction), (Karte(vm, 0).Suggestion, Karte(vm, 1).Suggestion));
            Assert.AreEqual((ScoreSuggestion.None, ScoreSuggestion.None), (Karte(vm, 2).Suggestion, Karte(vm, 3).Suggestion), "Blau bekommt etwas.");
            StringAssert.Contains(Karte(vm, 0).Grund, "Rot gewinnt das Stellvertreter-Duell");
            Assert.AreEqual("Stellvertreter für ✚ Rot: 200 ms – gewonnen", Karte(vm, 1).ReihenfolgeText);
            Assert.AreEqual($"✚ Rot schickt {Name(1)}: gewonnen", Karte(vm, 0).ReihenfolgeText);
            Assert.AreEqual($"★ Blau schickt {Name(blauer)}: verloren", Karte(vm, 5 - blauer).ReihenfolgeText);
            Assert.IsTrue(await WarteAsync(() => vm.ShowResultWindowCalls > 0), "Nach dem Ende geht das Bewertungsfenster nicht auf.");

            // Wiederholen: die gewertete Runde zählt nicht mehr, die Stellvertreter bleiben.
            await vm.WiederholeMinispielRundeAsync();
            Assert.AreEqual(ScoreSuggestion.None, Karte(vm, 0).Suggestion, "Nach dem Wiederholen bleibt der Vorschlag stehen.");
            Assert.AreEqual(string.Empty, Karte(vm, 1).ReihenfolgeText, "Nach dem Wiederholen steht der alte Satz noch da.");
            StringAssert.StartsWith(vm.Reaktion.Kopfzeile, "Runde 1 von 1", "Nach dem Wiederholen wird neu gewählt.");
        }

        [TestMethod]
        public async Task TheGameMasterDrawsForTeamsThatDoNotChoose()
        {
            var vm = await OeffneAsync();

            await vm.StartMinispielRundeAsync();
            await GibAsync(2, new { chosen = Id(2), lot = false });
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == "★ Blau" && z.Zeit == Name(2))));

            await vm.LoseStellvertreterAsync();

            Assert.IsTrue(vm.Reaktion.Kopfzeile.StartsWith("Runde 1 von 1"), "Nach dem Auslosen beginnt das Duell nicht: " + vm.Reaktion.Kopfzeile);
            var rot = vm.Reaktion.Zeilen.Single(z => z.Name == "✚ Rot").Zeit;
            Assert.IsTrue(rot == $"{Name(0)} (gelost)" || rot == $"{Name(1)} (gelost)", "Rot bekam keinen der Seinen gelost: " + rot);
            Assert.AreEqual(Name(2), vm.Reaktion.Zeilen.Single(z => z.Name == "★ Blau").Zeit, "Das Auslosen hat die Wahl von Blau überschrieben.");
            Assert.AreEqual(Visibility.Collapsed, vm.StellvertreterLosenVisibility);

            var prompt = (RecordingUserPrompt)UserPrompt.Current;
            await vm.LoseStellvertreterAsync();
            StringAssert.Contains(prompt.Informs.LastOrDefault()?.Message, "wählt kein Team", "Ein zweites Auslosen sagt nichts.");
        }

        /// <summary>Zwei Teams, Schere-Stein-Papier Best of 3: der Rundensieger am Beamer, nach 2:0 ist Schluss.</summary>
        [TestMethod]
        public async Task TwoTeamsPlayRockPaperScissorsBestOfThree()
        {
            var vm = await OeffneAsync(StellvertreterEvaluator.SchereSteinPapierVariant, runden: 3);

            await vm.StartMinispielRundeAsync();
            await GibAsync(1, new { chosen = Id(0), lot = false });
            await GibAsync(2, new { chosen = Id(3), lot = false });
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("Schere, Stein oder Papier")), "Das Duell beginnt nicht: " + vm.Reaktion.Kopfzeile);

            await GibAsync(0, new { move = (int)SchereSteinPapier.Stein });
            await GibAsync(3, new { move = (int)SchereSteinPapier.Schere });
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Runde 1 von 3")
                                                 && vm.Reaktion.Kopfzeile.Contains($"{Name(0)} gewinnt die Runde")),
                "Der Rundensieger fehlt am Beamer: " + vm.Reaktion.Kopfzeile);

            await vm.StartMinispielRundeAsync();
            await GibAsync(0, new { move = (int)SchereSteinPapier.Papier });
            await GibAsync(3, new { move = (int)SchereSteinPapier.Stein });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "2:0 bei Best of 3 endet nicht: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual($"Ergebnis – {Name(0)} gewinnt für ✚ Rot", vm.Reaktion.Kopfzeile);
            Assert.AreEqual("Stellvertreter für ✚ Rot: 2 Siege – gewonnen", Karte(vm, 0).ReihenfolgeText);
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(vm, 1).Suggestion);
        }
    }
}
