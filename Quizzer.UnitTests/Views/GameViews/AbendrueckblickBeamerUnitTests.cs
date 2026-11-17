using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Statistics;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Der Abendrückblick am Beamer (M37 E04): er kommt in der Siegerehrung an, die Beamer und Spielleiterleiste teilen
    /// (<see cref="UcStatsView"/>) - ohne tote Bindung, ein leerer Abschnitt erscheint nicht, und blättert der Spielleiter
    /// zurück, steht wieder der Sieger da. Gemessen am Baum, nicht an Bildpunkten.
    /// </summary>
    [TestClass]
    public class AbendrueckblickBeamerUnitTests
    {
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

        private static readonly Rueckblick Voll = new(
            [new RueckblickBestwert("Schnellste Reaktion", [Guid.NewGuid()], "Bert", "187 ms")],
            new RueckblickSpitze([Guid.NewGuid()], 3, "Anna – 3 Klaus"),
            [new RueckblickJoker(Guid.NewGuid(), JokerKind.Block, Guid.NewGuid(), "Bert: Sperre gegen Carl")],
            new RueckblickSpitze([Guid.NewGuid()], 7, "Carl – 7 richtige Antworten"));

        /// <summary>Eine Siegerehrung: ein Spiel, dessen einzige Zelle gespielt ist.</summary>
        private static StatsContext Siegerehrung()
        {
            var spieler = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Abend", Phase = 1, SuggestedPhases = 1 };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = Guid.NewGuid(), IsDone = true });
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler });

            var leiter = new GameMasterViewModel { Game = spiel };
            var stats = new StatsContext { GameMasterViewModel = leiter, Game = spiel };
            stats.PlayerStatsContextList = [new PlayerStatsContext { Player = spieler, Game = spiel, StatsContext = stats }];
            stats.UpdateScores(true);
            return stats;
        }

        private static StackPanel SiegerTafel(UcStatsView ansicht)
            => Descendants<StackPanel>(ansicht).First(s => s.GetBindingExpression(UIElement.VisibilityProperty)?.ParentBinding.Path.Path == nameof(StatsContext.SiegerVisibility));

        [TestMethod]
        public void TheReviewReachesTheBeamerWithoutDeadBindings()
        {
            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var stats = Siegerehrung();
                var ansicht = new UcStatsView { DataContext = stats };

                stats.SetzeRueckblick(new AbendrueckblickViewModel(Voll));
                stats.ZeigeTafel(SiegerehrungTafel.Rueckblick);
                Auslegen(ansicht);

                var rueckblick = Descendants<UcAbendrueckblickView>(ansicht).Single();
                var texte = Descendants<TextBlock>(rueckblick).Where(t => Sichtbar(t)).Select(t => t.Text).ToList();

                Assert.IsTrue(Sichtbar(rueckblick), "Der Rückblick steht nicht auf dem Beamer.");
                Assert.IsFalse(Sichtbar(SiegerTafel(ansicht)), "Sieger und Rückblick stehen übereinander.");
                CollectionAssert.AreEqual(new[]
                {
                    "Rückblick auf diesen Abend",
                    "Bestwerte des Abends", "Schnellste Reaktion: Bert – 187 ms",
                    "Meiste Klaus", "Anna – 3 Klaus",
                    "Eingelöste Joker", "Bert: Sperre gegen Carl",
                    "Meiste richtige Antworten", "Carl – 7 richtige Antworten",
                }, texte);
                Assert.AreEqual(0, wache.Errors.Count, "Bindungen des Rückblicks greifen ins Leere: " + string.Join(" || ", wache.Errors));
            });
        }

        [TestMethod]
        public void AnEmptySectionDoesNotAppear()
        {
            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var nurZwei = Voll with { MeisteKlaus = null, Joker = [] };
                var ansicht = new UcAbendrueckblickView { DataContext = new AbendrueckblickViewModel(nurZwei) };
                Auslegen(ansicht);

                Visibility Abschnitt(string name) => ((StackPanel)ansicht.FindName(name)).Visibility;

                Assert.AreEqual(Visibility.Visible, Abschnitt("BestwerteAbschnitt"));
                Assert.AreEqual(Visibility.Collapsed, Abschnitt("KlausAbschnitt"), "Ohne Klau steht die Überschrift „Meiste Klaus“ leer da.");
                Assert.AreEqual(Visibility.Collapsed, Abschnitt("JokerAbschnitt"), "Ohne eingelösten Joker steht die Überschrift leer da.");
                Assert.AreEqual(Visibility.Visible, Abschnitt("RichtigeAbschnitt"));
                Assert.AreEqual(0, wache.Errors.Count, string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>Gegenrichtung: zurückgeblättert steht der Sieger wieder da, und vor dem Spielende keine der beiden Tafeln.</summary>
        [TestMethod]
        public void BackToTheWinnerAndNothingBeforeTheEnd()
        {
            UiTestHost.Run(() =>
            {
                var stats = Siegerehrung();
                var ansicht = new UcStatsView { DataContext = stats };
                stats.SetzeRueckblick(new AbendrueckblickViewModel(Voll));
                stats.ZeigeTafel(SiegerehrungTafel.Rueckblick);
                stats.ZeigeTafel(SiegerehrungTafel.Sieger);
                Auslegen(ansicht);

                Assert.IsTrue(Sichtbar(SiegerTafel(ansicht)), "Zurückgeblättert fehlt der Sieger.");
                Assert.IsFalse(Sichtbar(Descendants<UcAbendrueckblickView>(ansicht).Single()));

                stats.ZeigeTafel(SiegerehrungTafel.Rueckblick);
                stats.Game.GameGridCoordinates[0].IsDone = false;
                stats.UpdateScores(false);
                Auslegen(ansicht);

                Assert.AreEqual(SiegerehrungTafel.Sieger, stats.Tafel, "Ein wieder offenes Spiel beginnt die nächste Siegerehrung nicht beim Sieger.");
                Assert.IsFalse(Sichtbar(Descendants<UcAbendrueckblickView>(ansicht).Single()), "Der Rückblick steht mitten im Spiel.");
                Assert.IsFalse(Sichtbar(SiegerTafel(ansicht)), "Die Siegerehrung steht mitten im Spiel über der Punkteleiste.");
            });
        }
    }
}
