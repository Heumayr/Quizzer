using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Statistics;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Der Punkteverlauf am Beamer (M37 E06): je Spieler eine Linie mit Name und Endstand daran, ohne tote Bindung, und
    /// gleich endende Linien behalten lesbare Namen. Die dritte Tafel der Siegerehrung - nur, wenn es einen Verlauf gibt.
    /// </summary>
    [TestClass]
    public class PunkteverlaufBeamerUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid(), Bert = Guid.NewGuid(), Carl = Guid.NewGuid();

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

        /// <summary>Ohne Fenster ist <c>IsVisible</c> immer falsch - also selbst nachsehen, ob es und alle Eltern sichtbar sind.</summary>
        private static bool Sichtbar(DependencyObject element)
        {
            for (var e = element; e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement { Visibility: not Visibility.Visible })
                    return false;
            }

            return true;
        }

        private static void Auslegen(FrameworkElement element)
        {
            element.Measure(new Size(1920, 1080));
            element.Arrange(new Rect(0, 0, 1920, 1080));
            element.UpdateLayout();
        }

        /// <summary>Drei Fragen: Anna führt, Bert und Carl enden gleich.</summary>
        private static Verlauf DreiSpieler() => new(
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [
                new VerlaufReihe(Anna, "Anna", [0, 200, 300, 500]),
                new VerlaufReihe(Bert, "Bert", [0, 100, -50, 150]),
                new VerlaufReihe(Carl, "Carl", [0, 0, 150, 150]),
            ]);

        [TestMethod]
        public void TheLeaderEndsHigherAndEveryLineCarriesItsName()
        {
            UiTestHost.Run(() =>
            {
                var vm = new PunkteverlaufViewModel(DreiSpieler());

                Assert.AreEqual(3, vm.Linien.Count);
                Assert.IsTrue(vm.Linien.All(l => l.Punkte.Count == 4), "Je Linie ein Punkt am Start und einer nach jeder Frage.");
                Assert.IsTrue(vm.Linien[0].EndeY < vm.Linien[1].EndeY, "Der Führende endet nicht weiter oben - die Achse steht auf dem Kopf.");
                Assert.IsTrue(vm.Linien[1].Punkte[2].Y > vm.NullLinieY, "Ein Minus liegt nicht unter der Nulllinie.");
                CollectionAssert.AreEqual(new[] { "Anna: 500", "Bert: 150", "Carl: 150" }, vm.Linien.Select(l => l.NameText).ToArray());
            });
        }

        /// <summary>Gleicher Endstand: die Namen stehen nicht übereinander, sondern untereinander - mit Verbinder zur Linie.</summary>
        [TestMethod]
        public void EqualEndScoresKeepTheirNamesApart()
        {
            UiTestHost.Run(() =>
            {
                var vm = new PunkteverlaufViewModel(DreiSpieler());
                var bert = vm.Linien.Single(l => l.Name == "Bert");
                var carl = vm.Linien.Single(l => l.Name == "Carl");

                Assert.AreEqual(bert.EndeY, carl.EndeY, "Vorbedingung: beide enden auf derselben Höhe.");
                Assert.IsTrue(Math.Abs(bert.NameOben - carl.NameOben) >= PunkteverlaufViewModel.Zeilenabstand,
                    $"Die Namen überdecken sich: {bert.NameOben} und {carl.NameOben}.");
                Assert.IsTrue(vm.Linien.All(l => l.NameOben >= 0 && l.NameOben <= PunkteverlaufViewModel.Hoehe - PunkteverlaufViewModel.Zeilenabstand),
                    "Ein Name ragt aus dem Zeichenfeld.");
            });
        }

        /// <summary>Die Siegerehrung zeigt den Verlauf als eigene Tafel - Linien und Namen kommen an, keine Bindung greift ins Leere.</summary>
        [TestMethod]
        public void TheChartReachesTheBeamerWithoutDeadBindings()
        {
            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var stats = Siegerehrung();
                var ansicht = new UcStatsView { DataContext = stats };

                stats.SetzeVerlauf(new PunkteverlaufViewModel(DreiSpieler()));
                stats.ZeigeTafel(SiegerehrungTafel.Verlauf);
                Auslegen(ansicht);

                var diagramm = Descendants<UcPunkteverlaufView>(ansicht).Single();
                var texte = Descendants<TextBlock>(diagramm).Select(t => t.Text).ToList();

                Assert.IsTrue(Sichtbar(diagramm), "Der Verlauf steht nicht auf dem Beamer.");
                Assert.AreEqual(3, Descendants<Polyline>(diagramm).Count(p => p.Points.Count == 4), "Nicht je Spieler eine Linie.");
                foreach (var name in new[] { "Anna: 500", "Bert: 150", "Carl: 150", "Punkteverlauf", "nach 3 Fragen" })
                    CollectionAssert.Contains(texte, name, $"„{name}“ fehlt am Beamer.");
                Assert.AreEqual(0, wache.Errors.Count, "Bindungen des Verlaufs greifen ins Leere: " + string.Join(" || ", wache.Errors));
            });
        }

        [TestMethod]
        public void TheChartIsAPageOnlyWhenThereIsOne()
        {
            var stats = new StatsContext();
            var rueckblick = new AbendrueckblickViewModel(new Rueckblick([], null, [], new RueckblickSpitze([Anna], 1, "Anna – 1 richtige Antwort")));

            stats.SetzeRueckblick(rueckblick);
            stats.SetzeVerlauf(new PunkteverlaufViewModel(DreiSpieler()));
            CollectionAssert.AreEqual(new[] { SiegerehrungTafel.Sieger, SiegerehrungTafel.Rueckblick, SiegerehrungTafel.Verlauf }, stats.Tafeln.ToArray());

            stats.ZeigeTafel(SiegerehrungTafel.Rueckblick);
            Assert.AreEqual("Punkteverlauf zeigen", stats.NaechsteTafelText);

            stats.SetzeVerlauf(null);
            CollectionAssert.AreEqual(new[] { SiegerehrungTafel.Sieger, SiegerehrungTafel.Rueckblick }, stats.Tafeln.ToArray(),
                "Ein altes Spiel ohne Zeitpunkte bekommt eine leere Tafel.");
            Assert.AreEqual("Sieger zeigen", stats.NaechsteTafelText);
        }

        /// <summary>Eine Siegerehrung: ein Spiel, dessen einzige Zelle gespielt ist.</summary>
        private static StatsContext Siegerehrung()
        {
            var spieler = new Player { Id = Anna, Designation = "Anna", DisplayName = "Anna" };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Abend", Phase = 1, SuggestedPhases = 1 };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = Guid.NewGuid(), IsDone = true });
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler });

            var stats = new StatsContext { GameMasterViewModel = new GameMasterViewModel { Game = spiel }, Game = spiel };
            stats.PlayerStatsContextList = [new PlayerStatsContext { Player = spieler, Game = spiel, StatsContext = stats }];
            stats.UpdateScores(true);
            return stats;
        }
    }
}
