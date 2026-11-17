using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Neuer Rekord!" am Beamer (M37 E02), gemessen am ViewModel: nach dem Ende steht die Ansage auf der Tafel, die Beamer
    /// und Spielleiter teilen, mit Ton - und das Bewertungsfenster geht trotzdem auf. Der bisherige Stand kommt aus der
    /// Datenbank, ohne die Zeilen dieser Zelle.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private static readonly DateTimeOffset FruehererAbend = new(2026, 1, 9, 20, 0, 0, TimeSpan.FromHours(1));

        /// <summary>Ein anderes Spiel mit einem gespeicherten Rohwert - der Stand aller Zeiten. Liefert das Aufraeumen.</summary>
        private static async Task<Func<Task>> FremderRekordAsync(string name, int wert, MinigameKind art = MinigameKind.Reaction, int spielweise = 0)
        {
            var kennung = Guid.NewGuid().ToString("N")[..6];
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Rekord " + kennung };
            var spieler = new Player { Id = Guid.NewGuid(), Designation = name + " " + kennung, DisplayName = name };
            var frage = new MinigameQuestion { Id = Guid.NewGuid(), Designation = art + " " + kennung, DesignationShort = "R", CategoryId = kategorie.Id, Points = 100 };
            MinigameProfiles.For(art).ApplyDefaults(frage);
            frage.Variant = spielweise;
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Frueherer Abend " + kennung, Phase = 1, SuggestedPhases = 1 };

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

            await GespielteZelleAsync(spiel.Id, frage.Id, spieler.Id, wert);

            return async () =>
            {
                using (var ctrl = new GamesController()) { await ctrl.DeleteAsync(spiel.Id); await ctrl.SaveChangesAsync(); }
                using (var ctrl = new PlayersController()) { await ctrl.DeleteAsync(spieler.Id); await ctrl.SaveChangesAsync(); }
                using (var ctrl = new QuestionBasesController()) { await ctrl.DeleteAsync(frage.Id); await ctrl.SaveChangesAsync(); }
                using (var ctrl = new CategoriesController()) { await ctrl.DeleteAsync(kategorie.Id); await ctrl.SaveChangesAsync(); }
            };
        }

        /// <summary>Eine weitere, schon gespielte Zelle mit einem gespeicherten Rohwert - im eigenen oder einem fremden Spiel.</summary>
        private static async Task GespielteZelleAsync(Guid spiel, Guid frage, Guid spieler, int wert)
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel, X = 1, Y = 0, Phase = 1, QuestionBaseId = frage, IsDone = true };

            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlErgebnis = new QuestionResultsController(ctrlZellen);
            await ctrlZellen.InsertAsync(zelle);
            await ctrlErgebnis.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = spiel, PlayerId = spieler, GameGridCoordinateId = zelle.Id, QuestionBaseId = frage,
                Score = 100, RecordValue = wert, RecordedAt = FruehererAbend,
            });
            await ctrlZellen.SaveChangesAsync();
        }

        private async Task SpieleReaktionAsync(TestableCurrentQuestionViewModel vm, int anna, int bert)
        {
            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { falseStart = false, milliseconds = anna });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = bert });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis") && vm.Reaktion.Rekord != null),
                $"Nach der Runde steht keine Rekordansage am Beamer: {vm.Reaktion.Kopfzeile} / {vm.Reaktion.Rekord}");
        }

        [TestMethod]
        public async Task ATimeBetterThanEverIsAnnouncedAsARecordOfAllTime()
        {
            var aufraeumen = await FremderRekordAsync("Carl", 270);
            try
            {
                var vm = await ReaktionsrundeAsync();
                Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Rekord != null), "Bert schlaegt Carls 270 ms - keine Ansage.");

                var bert = world!.Players[1];
                Assert.AreEqual("Neuer Rekord aller Zeiten!", vm.Reaktion.Rekord!.Titel);
                Assert.AreEqual($"{bert.CalculatedDisplayName} – 250 ms (bisher 270 ms, Carl)", vm.Reaktion.Rekord.Zeile);
                Assert.AreEqual(Visibility.Visible, vm.Reaktion.RekordVisibility);
                Assert.IsTrue(Gespielt("Rekord"), "Zum Rekord kam kein Ton.");

                // Die Ansage haelt nichts an: das Bewertungsfenster ging wie bisher auf, die Tafel steht darunter.
                Assert.AreEqual(1, vm.ShowResultWindowCalls, "Das Bewertungsfenster ging nicht auf.");
                Assert.AreEqual(bert.CalculatedDisplayName, vm.Reaktion.Zeilen[0].Name, "Die Ansage hat die Tafel ersetzt.");
            }
            finally
            {
                await aufraeumen();
            }
        }

        [TestMethod]
        public async Task ATimeBetterThanTonightButNotThanEverIsAnnouncedAsARecordOfTheEvening()
        {
            var aufraeumen = await FremderRekordAsync("Carl", 200);
            try
            {
                var vm = await OeffneReaktionAsync(runden: 1);
                var (anna, bert) = (world!.Players[0], world.Players[1]);
                await GespielteZelleAsync(world.Game.Id, world.Question.Id, anna.Id, 280);

                await SpieleReaktionAsync(vm, anna: 300, bert: 250);

                Assert.AreEqual("Neuer Rekord des Abends!", vm.Reaktion.Rekord!.Titel, "Carls 200 ms stehen noch - kein Rekord aller Zeiten.");
                Assert.AreEqual($"{bert.CalculatedDisplayName} – 250 ms (bisher 280 ms, {anna.DisplayName})", vm.Reaktion.Rekord.Zeile);
            }
            finally
            {
                await aufraeumen();
            }
        }

        /// <summary>Die Zelle ist gespeichert (Bert 250 ms) und wird wiederholt: 260 ms schlagen Carls 270 ms, nicht sich selbst.</summary>
        [TestMethod]
        public async Task TheCellsOwnSavedTimesDoNotCountWhenItIsPlayedAgain()
        {
            var aufraeumen = await FremderRekordAsync("Carl", 270);
            try
            {
                var vm = await ReaktionsrundeAsync();
                Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Rekord != null), "Die erste Runde bringt schon keine Ansage.");
                Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Speichern scheiterte.");

                await vm.WiederholeMinispielRundeAsync();
                Assert.IsNull(vm.Reaktion.Rekord, "Die Ansage bleibt ueber der neuen Runde stehen.");
                Assert.AreEqual(Visibility.Collapsed, vm.Reaktion.RekordVisibility);

                await AbgebenAsync(0, new { falseStart = false, milliseconds = 300 });
                await AbgebenAsync(1, new { falseStart = false, milliseconds = 260 });

                Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Rekord != null),
                    "Die Wiederholung misst sich an ihren eigenen gespeicherten 250 ms: " + vm.Reaktion.Kopfzeile);
                Assert.AreEqual($"{world!.Players[1].CalculatedDisplayName} – 260 ms (bisher 270 ms, Carl)", vm.Reaktion.Rekord!.Zeile);
            }
            finally
            {
                await aufraeumen();
            }
        }

        /// <summary>Kopfrechnen „mittel": 2,00 richtige je Minute schlagen 1,50 in mittel - Carls 9,00 in leicht zaehlen nicht.</summary>
        [TestMethod]
        public async Task AMentalArithmeticRecordCountsOnlyWithinItsDifficulty()
        {
            var leicht = await FremderRekordAsync("Carl", 900, MinigameKind.MentalArithmetic, KopfrechnenAufgaben.Leicht);
            var mittel = await FremderRekordAsync("Dora", 150, MinigameKind.MentalArithmetic, KopfrechnenAufgaben.Mittel);
            try
            {
                var vm = await OeffneMinispielAsync(MinigameKind.MentalArithmetic, f => { f.DurationSeconds = 30; f.Variant = KopfrechnenAufgaben.Mittel; });
                var spiel = serverVm!.BuzzerControlsViewModel!.MinigameState!;
                var uhr = new StellbareUhr();
                spiel.Uhr = uhr;
                spiel.Zufall = (_, _) => KopfrechnenStartwert;

                await vm.StartMinispielRundeAsync();
                uhr.Vor(TimeSpan.FromSeconds(3 + 1));
                var loesung = KopfrechnenAufgaben.Aufgabe(KopfrechnenStartwert, 1, KopfrechnenAufgaben.Mittel).Loesung;
                await AbgebenAsync(0, new { taskNumber = 1, answer = loesung }, MinigameKind.MentalArithmetic);
                uhr.Vor(TimeSpan.FromSeconds(30));

                Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Rekord != null),
                    "Keine Ansage - gemessen an leicht statt an mittel? " + vm.Reaktion.Kopfzeile);
                Assert.AreEqual($"{world!.Players[0].CalculatedDisplayName} – 2,00 richtige/min (bisher 1,50 richtige/min, Dora)",
                    vm.Reaktion.Rekord!.Zeile);
            }
            finally
            {
                await mittel();
                await leicht();
            }
        }
    }
}
