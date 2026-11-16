using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Heisse Kartoffel beim Spielleiter (M35 C6), auf demselben Pruefstand wie die Reaktion: am Beamer nur, wer haelt -
    /// nie eine Aussage, nie eine Zeit; eine Weitergabe tickt. Es knallt ueber den Server, danach die Vorschlaege (E1):
    /// −B fuer den Halter, +B fuer den, der sie ihm zugeschoben hat, und der Verlauf samt Wahrheit als Aufloesung.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        /// <summary>Der Pruefstand legt zwei Zeilen „Hinweis 1" und „Hinweis 2" an, beide ohne Haken - also falsch.</summary>
        [TestMethod]
        public async Task HotPotatoShowsOnlyTheHolderAndTheBangCostsTheHolderWhatThePasserWins()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.HotPotato, f => f.Share = 50, ausloeser: 0);
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;
            spiel.Zufall = (a, _) => a; // der kuerzeste Zuender: ein Drittel von 60 s

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            bool BeamerNennt(string text) => vm.Reaktion.Kopfzeile.Contains(text) || vm.Reaktion.Zeilen.Any(z => z.Name.Contains(text) || z.Zeit.Contains(text));
            var betrag = Punkteanteil.Von(Feldpunkte(vm), 50);

            Assert.AreEqual(Visibility.Visible, vm.MinispielPaarVisibility, "Wer zuerst haelt, ist nicht waehlbar.");
            Assert.AreEqual(Visibility.Collapsed, vm.PaarPartnerVisibility, "Die Kartoffel fragt nach einem Gegenueber.");

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, $"{anna.CalculatedDisplayName} hält sie");
            Assert.AreEqual("hält die Kartoffel!", vm.Reaktion.Zeilen.Single(z => z.Name == anna.CalculatedDisplayName).Zeit);

            uhr.Vor(TimeSpan.FromSeconds(3 + 1));
            await AbgebenAsync(1, new { statementNumber = 1, answer = false }, MinigameKind.HotPotato);
            await AbgebenAsync(0, new { statementNumber = 1, answer = false }, MinigameKind.HotPotato);
            await AbgebenAsync(0, new { statementNumber = 1, passTo = bert.Id }, MinigameKind.HotPotato);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains($"{bert.CalculatedDisplayName} hält sie")),
                "Die Weitergabe erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(Gespielt("Tick"), "Zur Weitergabe kam kein Ton.");
            Assert.AreEqual("1× weitergegeben", vm.Reaktion.Zeilen.Single(z => z.Name == anna.CalculatedDisplayName).Zeit);
            Assert.AreEqual("hält die Kartoffel!", vm.Reaktion.Zeilen.Single(z => z.Name == bert.CalculatedDisplayName).Zeit, "Am Beamer haelt nicht Bert.");
            Assert.IsFalse(BeamerNennt("Hinweis"), "Am Beamer steht eine Aussage.");
            Assert.AreEqual(KartoffelTafel.Kopfzeile(bert.CalculatedDisplayName), vm.Reaktion.Kopfzeile, "In der Kopfzeile steht mehr als der Halter - eine Zeit verriete den Zuender.");
            Assert.AreEqual(0, vm.Reaktion.Aufloesung.Count, "Waehrend der Runde steht die Aufloesung am Beamer.");
            Assert.IsFalse(Gespielt("Absturz"), "Vor dem Knall kam sein Ton.");

            uhr.Vor(TimeSpan.FromSeconds(20));

            Assert.IsTrue(await WarteAsync(() => spiel.Phase == MinigamePhase.Finished && vm.Reaktion.Kopfzeile.Contains("BUMM")),
                "Der Knall erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            StringAssert.Contains(vm.Reaktion.Kopfzeile, $"nach 20 s! Die Kartoffel ist bei {bert.CalculatedDisplayName} geplatzt");
            Assert.IsTrue(Gespielt("Absturz"), "Zum Knall kam nicht sein Ton.");

            Assert.AreEqual(2, vm.Reaktion.Aufloesung.Count, "Nach dem Knall steht der Verlauf nicht am Beamer: " + string.Join(" | ", vm.Reaktion.Aufloesung));
            StringAssert.StartsWith(vm.Reaktion.Aufloesung[0], "✗ falsch – Hinweis");
            StringAssert.EndsWith(vm.Reaktion.Aufloesung[0], $"{anna.CalculatedDisplayName}: richtig, weiter an {bert.CalculatedDisplayName}");
            StringAssert.EndsWith(vm.Reaktion.Aufloesung[1], $"{bert.CalculatedDisplayName}: offen beim Knall");

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(bert).Suggestion);
            Assert.AreEqual(-betrag, Karte(bert).CurrentScoreManipulation, "Der Halter verliert nicht den Anteil.");
            StringAssert.Contains(Karte(bert).SuggestionText, "beim Knall gehalten");
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(betrag, Karte(anna).CurrentScoreManipulation, "Wer zugeschoben hat, gewinnt nicht den Anteil.");
            Assert.IsTrue(vm.Reaktion.Zeilen.Single(z => z.Name == anna.CalculatedDisplayName).Sieger, "Am Beamer ist der Gewinner nicht markiert.");
            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
