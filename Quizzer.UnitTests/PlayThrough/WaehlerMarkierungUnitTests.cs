using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews;
using System.ComponentModel;
using System.Windows;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Wer als Nächstes die Frage wählt, ist auf der Punktekachel und am Beamer markiert.
    /// <para>
    /// <b>Nutzerwort vom 2026-10-23 (M30):</b> „wer als nächstes die frage wählt soll auch am
    /// spieler bildschirm markiert werden" - gewählt: Plakette „wählt" auf der Kachel und ein
    /// Hinweis über dem Raster. Bis dahin stand es nur als Text im Spielleiterfenster, und für den
    /// Wählerwechsel gab es keine einzige Zusicherung.
    /// </para>
    /// </summary>
    [TestClass]
    public class WaehlerMarkierungUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            TestEnvironment.ClearSwallowedExceptions();
            TestEnvironment.ClearDiscardedChanges();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 3);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
            TestEnvironment.ClearDiscardedChanges();
        }

        /// <summary>Nach dem Öffnen trägt genau die Kachel des Wählers die Plakette, und der Beamer nennt ihn.</summary>
        [TestMethod]
        public async Task AfterOpeningExactlyTheChooserIsMarked()
        {
            var vm = await OpenAsync();
            var waehler = vm.ChoosingPlayerItem;
            Assert.IsNotNull(waehler, "Nach dem Öffnen wählt niemand.");

            var markiert = vm.StatsContext.PlayerStatsContextList.Where(k => k.IsChoosing).ToList();
            Assert.AreEqual(1, markiert.Count, "Es trägt nicht genau eine Kachel die Plakette.");
            Assert.AreEqual(waehler!.Id, markiert[0].Player!.Id, "Die Plakette steht auf der falschen Kachel.");
            Assert.AreEqual(Visibility.Visible, markiert[0].ChoosingVisibility);
            Assert.IsTrue(vm.StatsContext.PlayerStatsContextList.Where(k => !k.IsChoosing).All(k => k.ChoosingVisibility == Visibility.Collapsed));

            Assert.AreEqual($"{waehler.CalculatedDisplayName} wählt die nächste Frage", vm.GamePlayerViewModel.WaehlerHinweis);
            Assert.AreEqual(Visibility.Visible, vm.GamePlayerViewModel.WaehlerHinweisVisibility);
        }

        /// <summary>
        /// Ein Wechsel des Wählers erreicht Kachel und Beamer. Geprüft wird die Meldung selbst, nicht
        /// nur der Endzustand - ohne sie zeigte die Oberfläche weiter den vorigen Wähler.
        /// </summary>
        [TestMethod]
        public async Task AChangeOfChooserReachesTileAndBeamer()
        {
            var vm = await OpenAsync();
            var vorher = vm.ChoosingPlayerItem!;
            var neu = vm.Players.First(p => p.Id != vorher.Id);
            var kachelNeu = vm.StatsContext.PlayerStatsContextList.Single(k => k.Player!.Id == neu.Id);
            var kachelVorher = vm.StatsContext.PlayerStatsContextList.Single(k => k.Player!.Id == vorher.Id);

            var meldungen = new List<string>();
            PropertyChangedEventHandler mitschreiben(string wer) => (_, e) => meldungen.Add($"{wer}.{e.PropertyName}");
            kachelNeu.PropertyChanged += mitschreiben("neu");
            kachelVorher.PropertyChanged += mitschreiben("vorher");
            vm.GamePlayerViewModel.PropertyChanged += mitschreiben("beamer");

            await vm.SetNextChoosingPlayer(new List<Player> { neu });

            Assert.AreEqual(neu.Id, vm.ChoosingPlayerItem?.Id, "Der Gewinner der Zelle wählt nicht als Nächster.");
            Assert.IsTrue(kachelNeu.IsChoosing && !kachelVorher.IsChoosing, "Die Plakette ist nicht umgezogen.");
            CollectionAssert.IsSubsetOf(new[] { "neu.ChoosingVisibility", "vorher.ChoosingVisibility", "beamer.WaehlerHinweis" }, meldungen,
                "Nicht jede Anzeige erfuhr vom Wechsel: " + string.Join(", ", meldungen));
            StringAssert.StartsWith(vm.GamePlayerViewModel.WaehlerHinweis, neu.CalculatedDisplayName);
        }

        private async Task<GameMasterViewModel> OpenAsync()
        {
            // Der Builder setzt keinen Spielleiter - ohne ihn öffnet sich kein Spiel. Am Spiel aus
            // dem Speicher, wie in GameMasterLoadUnitTests.SetModeratorAsync.
            world.Game.ModeratorPlayerId = world.Players[0].Id;
            using (var ctrl = new Quizzer.Logic.Controller.TypedControllers.GamesController())
            {
                await ctrl.UpdateAsync(world.Game);
                await ctrl.SaveChangesAsync();
            }

            var vm = new GameMasterViewModel();
            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNotNull(geladen, "Das Spiel liess sich nicht öffnen: " + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            // Wie im Programm: das Spielleiterfenster lädt, bevor der Beamer aufgeht - erst dabei
            // bekommt das Beamer-ViewModel seinen Spielleiter (OnloadAsync).
            await vm.LoadForTestAsync();
            return vm;
        }
    }
}
