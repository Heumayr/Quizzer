using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.ViewModels;
using Quizzer.Views;
using Quizzer.Views.GameViews;
using Quizzer.Views.HelperViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Sonderregeln im Spielaufbau und im Raster (M35 C3, V3): zufaellig verteilen nur auf erlaubte Zellen,
    /// mit Rueckfrage vor dem Ersetzen; kein Minispiel nimmt eine Regel; der Beamer zeigt nur angekuendigte.
    /// </summary>
    [TestClass]
    public class SonderregelAufbauUnitTests
    {
        private TestGameBuilder? world;
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public async Task TearDown()
        {
            if (world != null)
                await world.DisposeAsync();

            UserPrompt.Reset();
        }

        [TestMethod]
        public async Task DistributingHitsOnlyTheQuestionCellPersistsAndAsksBeforeReplacing()
        {
            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1);
            var vm = new EditGameViewModel();
            await vm.LoadModel(world.Game.Id);

            var mitFrage = vm.Zellen.Single(z => z.Id == world.Coordinate.Id);
            var leer = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = world.Game.Id, X = 7 };
            vm.Zellen.Add(leer);

            vm.Zufall = (von, bis) => von;
            vm.SonderregelAnzahl = 3;
            vm.SonderregelnAngekuendigt = true;
            await vm.VerteileSonderregelnAsync();

            Assert.AreEqual(CellSpecialRule.Stake, mitFrage.SpecialRule, "Die Zelle mit Frage bekam keine Regel.");
            Assert.IsTrue(mitFrage.SpecialRuleAnnounced);
            Assert.AreEqual(CellSpecialRule.None, leer.SpecialRule, "Eine leere Zelle bekam eine Regel.");
            Assert.AreEqual(0, prompt.Confirms.Count, "Gefragt, obwohl noch keine Regel lag.");
            StringAssert.Contains(prompt.Informs.Single().Message, "Nur 1 Zellen");

            using (var ctrl = new GameGridCoordinatesController())
            {
                var gespeichert = await ctrl.GetAsync(mitFrage.Id);
                Assert.AreEqual(CellSpecialRule.Stake, gespeichert!.SpecialRule, "Die Regel steht nur in der Maske.");
                Assert.IsTrue(gespeichert.SpecialRuleAnnounced, "Die Ankuendigung steht nur in der Maske.");
            }

            // --- Es liegt schon eine: nein laesst alles, ja ersetzt ---
            vm.Zufall = (von, bis) => bis - 1;
            prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;
            await vm.VerteileSonderregelnAsync();

            Assert.AreEqual(1, prompt.Confirms.Count, "Vor dem Ersetzen wurde nicht gefragt.");
            Assert.AreEqual(CellSpecialRule.Stake, mitFrage.SpecialRule, "Ohne Zustimmung ersetzt.");

            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            await vm.VerteileSonderregelnAsync();

            Assert.AreEqual(CellSpecialRule.Risk, mitFrage.SpecialRule, "Mit Zustimmung nicht ersetzt.");
        }

        [TestMethod]
        public void AMinigameCellTakesNoRuleAnOrdinaryOneDoes()
        {
            var vm = new QuestionSelectorViewModel();
            var minispiel = new GameGridCoordinate { Id = Guid.NewGuid(), QuestionBase = new MinigameQuestion { Id = Guid.NewGuid() } };
            var einsatz = vm.SonderregelOptionen.Single(o => o.Regel == CellSpecialRule.Stake);

            vm.SetDependencys(new Game(), minispiel);
            vm.GewaehlteSonderregel = einsatz;
            vm.SonderregelAngekuendigt = true;

            Assert.IsFalse(vm.SonderregelMoeglich);
            Assert.AreEqual(CellSpecialRule.None, minispiel.SpecialRule, "Das Minispiel nahm eine Sonderregel.");
            Assert.IsFalse(minispiel.SpecialRuleAnnounced);
            StringAssert.Contains(vm.SonderregelHinweis, "Minispiel");

            var gewoehnlich = new GameGridCoordinate { Id = Guid.NewGuid(), QuestionBase = new DefaultQuestion { Id = Guid.NewGuid() } };
            vm.SetDependencys(new Game(), gewoehnlich);
            vm.GewaehlteSonderregel = einsatz;
            vm.SonderregelAngekuendigt = true;

            Assert.AreEqual(CellSpecialRule.Stake, gewoehnlich.SpecialRule, "Die gewoehnliche Frage nahm die Regel nicht.");
            Assert.IsTrue(gewoehnlich.SpecialRuleAnnounced);
        }

        /// <summary>Am selben Zell-ViewModel: das Steuerelement entscheidet, ob eine verdeckte Regel zu sehen ist.</summary>
        [TestMethod]
        public void TheBeamerCellHidesWhatTheMasterCellShows()
        {
            var funde = new Dictionary<string, (string Text, Visibility Sicht)>();

            UiTestHost.Run(() =>
            {
                foreach (var (name, angekuendigt, spielleiter) in new[]
                         {
                             ("verdeckt am Beamer", false, false),
                             ("verdeckt beim Spielleiter", false, true),
                             ("angekuendigt am Beamer", true, false),
                         })
                {
                    var zelle = new GameGridCoordinate
                    {
                        Id = Guid.NewGuid(), QuestionBase = new DefaultQuestion { Id = Guid.NewGuid() },
                        SpecialRule = CellSpecialRule.Stake, SpecialRuleAnnounced = angekuendigt,
                    };
                    var steuer = new UcCellViewPlay
                    {
                        DataContext = new GameGridCoordinateViewModel(zelle) { CellView = CellView.Master },
                        ZeigtVerdeckteRegeln = spielleiter,
                    };

                    steuer.Measure(new Size(200, 150));
                    steuer.Arrange(new Rect(0, 0, 200, 150));
                    steuer.UpdateLayout();

                    var marke = (TextBlock)steuer.FindName("SonderregelMarke");
                    funde[name] = (marke.Text, marke.Visibility);
                }
            });

            Assert.AreEqual(Visibility.Collapsed, funde["verdeckt am Beamer"].Sicht, "Der Beamer zeigt eine verdeckte Regel.");
            Assert.AreEqual(string.Empty, funde["verdeckt am Beamer"].Text);
            Assert.AreEqual(Visibility.Visible, funde["verdeckt beim Spielleiter"].Sicht, "Der Spielleiter sieht die verdeckte Regel nicht.");
            StringAssert.Contains(funde["verdeckt beim Spielleiter"].Text, "verdeckt");
            Assert.AreEqual(Visibility.Visible, funde["angekuendigt am Beamer"].Sicht, "Eine angekuendigte Regel fehlt am Beamer.");
            StringAssert.Contains(funde["angekuendigt am Beamer"].Text, "Einsatz");
        }

        /// <summary>Nur das Spielleiter-Fenster schaltet die verdeckten Regeln ein - der Beamer bleibt bei der Vorgabe.</summary>
        [TestMethod]
        public void OnlyTheMasterWindowShowsHiddenRules()
        {
            bool? spielleiter = null;
            bool? beamer = null;

            UiTestHost.Run(() =>
            {
                spielleiter = ZeigtVerdeckte(new GameMasterView());
                beamer = ZeigtVerdeckte(new GamePlayerView());
            });

            Assert.AreEqual(true, spielleiter, "Das Spielleiter-Raster zeigt keine verdeckten Regeln.");
            Assert.AreEqual(false, beamer, "Das Beamer-Raster zeigt verdeckte Regeln.");
        }

        private static bool? ZeigtVerdeckte(Window fenster)
        {
            try
            {
                var inhalt = (FrameworkElement)fenster.Content;
                inhalt.Measure(new Size(1200, 800));
                inhalt.Arrange(new Rect(0, 0, 1200, 800));
                inhalt.UpdateLayout();

                return Descendants<ItemsControl>(inhalt)
                    .Select(liste => liste.ItemTemplate?.LoadContent())
                    .OfType<UcCellViewPlay>()
                    .Select(zelle => (bool?)zelle.ZeigtVerdeckteRegeln)
                    .FirstOrDefault();
            }
            finally
            {
                fenster.Close();
            }
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }
    }
}
