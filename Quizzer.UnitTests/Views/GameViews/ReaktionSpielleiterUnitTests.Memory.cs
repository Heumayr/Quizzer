using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Memory beim Spielleiter (M35 C5), auf demselben Pruefstand wie die Reaktion: jedes Umdrehen treibt die
    /// Paare am Beamer - nie ein Motiv -, wer fertig ist, bekommt einen Ton, und wer zuerst fertig ist, die
    /// Feldpunkte als Vorschlag mit Grund (E1).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        [TestMethod]
        public async Task MemoryCountsPairsOnTheBeamerAndProposesTheFieldPointsToTheFirstToFinish()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.Memory, f => { f.DurationSeconds = 60; f.Variant = 0; });
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;

            // Paar k liegt auf den Karten 2k und 2k+1 (MemoryBelegung.Mische, Grundordnung).
            spiel.Zufall = (von, bis) => bis == MemoryBelegung.Motive.Count ? von : bis - 1;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);

            async Task DreheAsync(int telefon, int karte)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(250));
                await AbgebenAsync(telefon, new { card = karte }, MinigameKind.Memory);
            }

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Memory läuft");
            uhr.Vor(TimeSpan.FromSeconds(3));

            await DreheAsync(1, 0);
            await DreheAsync(1, 1);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == bert.CalculatedDisplayName && z.Zeit == "1 von 6 Paaren")),
                "Berts Paar erscheint nicht am Beamer.");

            for (var k = 0; k < 12; k++)
                await DreheAsync(0, k);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == anna.CalculatedDisplayName && z.Zeit.StartsWith("✓ alle 6 Paare in"))),
                "Dass Anna fertig ist, erscheint nicht am Beamer.");
            Assert.IsTrue(Gespielt("Tick"), "Zum Fertigwerden kam kein Ton.");
            Assert.IsFalse(vm.Reaktion.Zeilen.Any(z => MemoryBelegung.Motive.Any(m => z.Zeit.Contains(m.Symbol) || z.Zeit.Contains(m.Name))),
                "Am Beamer steht ein Motiv - dort schauen alle mit.");

            uhr.Vor(TimeSpan.FromSeconds(60));

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("wer zuerst fertig ist")),
                "Das Ende erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(vm.Reaktion.Zeilen[0].Sieger && vm.Reaktion.Zeilen[0].Name == anna.CalculatedDisplayName, "Anna steht nicht als Siegerin vorn.");
            Assert.IsTrue(Gespielt("Ende"), "Zum Ende kam kein Ton.");

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(Feldpunkte(vm), Karte(anna).CurrentScoreManipulation, "Wer zuerst fertig ist, bekommt nicht die Feldpunkte.");
            StringAssert.Contains(Karte(anna).SuggestionText, "alle 6 Paare in");
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion, "Wer nicht vorn ist, bekommt einen Vorschlag.");
            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
