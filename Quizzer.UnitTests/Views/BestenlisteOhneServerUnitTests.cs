using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Server;
using Quizzer.Views.Bestenliste;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Bestenliste ohne Server (M37 E03): das Fenster baut im Einzelbetrieb aus der lokalen Datenbank auf und zeigt
    /// dieselben Kacheln wie „Meine Statistik".
    /// </summary>
    [TestClass]
    public class BestenlisteOhneServerUnitTests
    {
        private static readonly DateTimeOffset Frueh = new(2001, 1, 1, 20, 0, 0, TimeSpan.Zero);

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

        /// <summary>Ein gespieltes Stoppuhr-Feld mit einem Treffer auf die Millisekunde - frueh, damit es bei Gleichstand vorn steht.</summary>
        private static async Task<(Player Spieler, Game Spiel)> StoppuhrAbendAsync()
        {
            var kennung = Guid.NewGuid().ToString("N")[..6];
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Bestenliste " + kennung };
            var spieler = new Player { Id = Guid.NewGuid(), Designation = "Uhrwerk " + kennung };
            var frage = new MinigameQuestion { Id = Guid.NewGuid(), Designation = "Stoppuhr " + kennung, DesignationShort = "S", CategoryId = kategorie.Id, Points = 100 };
            MinigameProfiles.For(MinigameKind.Stopwatch).ApplyDefaults(frage);
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Abend " + kennung, Phase = 1, SuggestedPhases = 1 };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }
            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(spieler);
                await ctrl.SaveChangesAsync();
            }
            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }
            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1, QuestionBaseId = frage.Id, IsDone = true };
            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlErgebnis = new QuestionResultsController(ctrlZellen);
            await ctrlZellen.InsertAsync(zelle);
            await ctrlErgebnis.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = frage.Id,
                Score = 100, RecordValue = 0, RecordedAt = Frueh,
            });
            await ctrlZellen.SaveChangesAsync();

            return (spieler, spiel);
        }

        [TestMethod]
        public async Task TheViewBuildsWithoutAServerFromTheLocalGames()
        {
            Assert.IsFalse(ServerSession.IsActive, "Der Test braucht den Einzelbetrieb.");

            var (spieler, spiel) = await StoppuhrAbendAsync();

            var vm = new BestenlisteViewModel();
            await vm.LoadForTestAsync();

            Assert.AreEqual(BestenlisteViewModel.LokalText, vm.Herkunft, "Ohne Server kommt die Liste nicht aus der lokalen Datenbank.");

            UiTestHost.Run(() =>
            {
                var fenster = new BestenlisteView { DataContext = vm };
                var inhalt = (FrameworkElement)fenster.Content;
                inhalt.Measure(new Size(fenster.Width, fenster.Height));
                inhalt.Arrange(new Rect(0, 0, fenster.Width, fenster.Height));
                inhalt.UpdateLayout();

                var texte = Descendants<TextBlock>(inhalt).Select(t => t.Text).ToList();
                var titel = MinigameProfiles.All
                    .Where(p => p.Record != null)
                    .SelectMany(p => p.Record!.VariantTitles.Count == 0
                        ? [$"{p.Record.Title} ({p.DisplayName})"]
                        : p.Record.VariantTitles.Select(v => $"{p.Record.Title} – {v} ({p.DisplayName})"))
                    .Append("Meiste Klaus");

                foreach (var t in titel)
                    CollectionAssert.Contains(texte, t, $"Die Kachel {t} fehlt.");

                Assert.IsTrue(texte.Any(t => t.Contains(spieler.Designation) && t.Contains("genau getroffen") && t.Contains(spiel.Designation)),
                    "Der lokale Rekord steht nicht in der Stoppuhr-Kachel. Gelesen: " + string.Join(" | ", texte));
            });
        }
    }
}
