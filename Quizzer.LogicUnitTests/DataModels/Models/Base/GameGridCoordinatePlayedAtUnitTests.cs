using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.DataModels.Models.Base
{
    /// <summary>
    /// Wann eine Zelle gespielt wurde (M37 E06): der erste Abschluss stempelt, ein erneuter laesst den Stempel, Oeffnen und
    /// Zuruecksetzen nehmen ihn weg. Die Ruecknahme steht in <c>LetzteWertungUnitTests</c>.
    /// </summary>
    [TestClass]
    public class GameGridCoordinatePlayedAtUnitTests
    {
        private static readonly DateTimeOffset Erst = new(2026, 11, 14, 20, 15, 0, TimeSpan.FromHours(1));
        private static readonly DateTimeOffset Spaeter = Erst.AddMinutes(40);

        [TestMethod]
        public void TheFirstCompletionStampsTheCell()
        {
            var zelle = new GameGridCoordinate();

            zelle.SetDone(true, Erst);

            Assert.IsTrue(zelle.IsDone);
            Assert.AreEqual(Erst, zelle.PlayedAt);
        }

        /// <summary>„Abschließen" an einer schon gespielten Zelle (nachgebessert) - sie bleibt an ihrem Platz im Verlauf.</summary>
        [TestMethod]
        public void CompletingAgainKeepsTheFirstStamp()
        {
            var zelle = new GameGridCoordinate();
            zelle.SetDone(true, Erst);

            zelle.SetDone(true, Spaeter);

            Assert.AreEqual(Erst, zelle.PlayedAt, "Die nachgebesserte Zelle rueckt ans Ende des Verlaufs.");
        }

        /// <summary>Eine vor E06 gespielte Zelle bekommt beim Nachbessern keinen erfundenen Zeitpunkt - sonst stuende sie am Ende.</summary>
        [TestMethod]
        public void ACellPlayedBeforeTheColumnStaysWithoutStamp()
        {
            var zelle = new GameGridCoordinate { IsDone = true };

            zelle.SetDone(true, Spaeter);

            Assert.IsNull(zelle.PlayedAt);
        }

        [TestMethod]
        public void OpeningTheCellTakesTheStampAway()
        {
            var zelle = new GameGridCoordinate();
            zelle.SetDone(true, Erst);

            zelle.SetDone(false, Spaeter);

            Assert.IsFalse(zelle.IsDone);
            Assert.IsNull(zelle.PlayedAt);

            zelle.SetDone(true, Spaeter);
            Assert.AreEqual(Spaeter, zelle.PlayedAt, "Wieder abgeschlossen, zaehlt der neue Zeitpunkt.");
        }

        /// <summary>„Alle Ergebnisse loeschen" setzt die Zellen zurueck - und ihren Zeitpunkt mit, sonst begaenne der neue Abend mit Stempeln.</summary>
        [TestMethod]
        public async Task ResettingTheGameClearsTheStamps()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Verlauf zuruecksetzen" };
            var frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Zeitpunkt", DesignationShort = "Z", CategoryId = kategorie.Id, Points = 100 };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Verlauf " + Guid.NewGuid().ToString("N")[..6], Phase = 1, SuggestedPhases = 1 };
            var anderes = new Game { Id = Guid.NewGuid(), Designation = "Anderes " + Guid.NewGuid().ToString("N")[..6], Phase = 1, SuggestedPhases = 1 };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
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
                await ctrl.UpsertAsync(anderes);
                await ctrl.SaveChangesAsync();
            }

            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = frage.Id, IsDone = true, PlayedAt = Erst };
            var fremde = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = anderes.Id, QuestionBaseId = frage.Id, IsDone = true, PlayedAt = Erst };
            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(zelle);
                await ctrl.InsertAsync(fremde);
                await ctrl.SaveChangesAsync();
                await ctrl.UpdateIsDoneStateOfGame(spiel.Id, false);
            }

            using var lesen = new GameGridCoordinatesController();
            var zurueck = await lesen.GetAsync(zelle.Id);
            var unberuehrt = await lesen.GetAsync(fremde.Id);

            Assert.IsFalse(zurueck!.IsDone);
            Assert.IsNull(zurueck.PlayedAt, "Die zurueckgesetzte Zelle traegt noch den Zeitpunkt des alten Abends.");
            Assert.AreEqual(Erst, unberuehrt!.PlayedAt, "Das Zuruecksetzen hat ein anderes Spiel angefasst.");
        }
    }
}
