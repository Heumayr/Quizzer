using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Der Zocker beim Spielleiter (M35 C4), auf demselben Pruefstand wie die Reaktion: der Multiplikator am
    /// Beamer kommt von der Serveruhr, der Server schliesst am Absturz, die Karten bekommen Gewinn und
    /// verlorenen Einsatz mit Grund (E1), und der Absturz hat seinen eigenen Ton (V5).
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        /// <summary>Wurf fuer den Absturz bei ×2,40.</summary>
        private const int ZockerWurf240 = 5834;

        /// <summary>Eine Serveruhr zum Vorstellen - steht still, bis der Test sie bewegt.</summary>
        private sealed class StellbareUhr : TimeProvider
        {
            private long ticks = TimeSpan.FromHours(1).Ticks;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp() => Interlocked.Read(ref ticks);

            public void Vor(TimeSpan dauer) => Interlocked.Add(ref ticks, dauer.Ticks);
        }

        [TestMethod]
        public async Task TheCrashRisesOnTheBeamerAndProposesTheWinAndTheLostStake()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.Crash, f => { f.Variant = ZockerEvaluator.MitEinsatzVariant; f.Share = 50; });
            var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
            var uhr = new StellbareUhr();
            spiel.Uhr = uhr;
            spiel.Zufall = (_, _) => ZockerWurf240;

            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            var einsatz = Punkteanteil.Von(Feldpunkte(vm), 50);

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "gleich steigt", "Im Vorlauf steht schon ein Multiplikator.");
            Assert.AreEqual(2, vm.Reaktion.Zeilen.Count(z => z.Zeit == "noch drin"), "Am Beamer stehen nicht beide als „noch drin“.");

            uhr.Vor(TimeSpan.FromSeconds(3 + 2));
            await AbgebenAsync(0, new { cashOut = true }, MinigameKind.Crash);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Any(z => z.Name == anna.CalculatedDisplayName && z.Zeit.Contains("×1,28"))),
                "Der Ausstieg erscheint nicht am Beamer.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "×1,28", "Der Multiplikator am Beamer kommt nicht von der Serveruhr.");
            Assert.IsTrue(Gespielt("Tick"), "Zum Ausstieg kam kein Ton.");

            uhr.Vor(ZockerEvaluator.Absturzzeit(240));

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("abgestürzt bei ×2,40")),
                "Der Absturz erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(Gespielt("Absturz"), "Zum Absturz kam nicht sein Ton.");

            var gewinn = (int)Math.Round(einsatz * 28 / 100.0, MidpointRounding.AwayFromZero);
            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(gewinn, Karte(anna).CurrentScoreManipulation, "Der Ausstieg bringt nicht Einsatz × (Multiplikator − 1).");
            StringAssert.Contains(Karte(anna).SuggestionText, "bei ×1,28 ausgestiegen");
            Assert.AreEqual(128, Karte(anna).Messwert, "Der Ausstieg steht nicht als Messwert auf der Karte (M35 C9).");
            Assert.IsNull(Karte(bert).Messwert, "Wer abstuerzte, hat einen Ausstieg.");
            Assert.AreEqual(-einsatz, Karte(bert).CurrentScoreManipulation, "Der Absturz kostet nicht den Einsatz.");
            StringAssert.Contains(Karte(bert).SuggestionText, "Einsatz verloren");
            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }
    }
}
