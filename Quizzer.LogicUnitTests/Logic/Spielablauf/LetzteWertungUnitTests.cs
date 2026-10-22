using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Spielablauf;

namespace Quizzer.LogicUnitTests.Logic.Spielablauf
{
    /// <summary>V5: die letzte Wertung zurücknehmen - im Speicher und in der Datenbank.</summary>
    [TestClass]
    public class LetzteWertungUnitTests
    {
        private Player anna = null!;
        private Player ben = null!;
        private Game spiel = null!;
        private GameGridCoordinate zelle = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Wertung" };
            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            var frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Hauptstadt", DesignationShort = "H", CategoryId = kategorie.Id, Points = 200 };
            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            ben = new Player { Id = Guid.NewGuid(), Designation = "Ben", DisplayName = "Ben" };
            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(anna);
                await ctrl.InsertAsync(ben);
                await ctrl.SaveChangesAsync();
            }

            spiel = new Game { Id = Guid.NewGuid(), Designation = "Wertung", Phase = 1, SuggestedPhases = 1, CurrentChoosingPlayerId = anna.Id, RegularChoosingPlayerId = anna.Id };
            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, Game = spiel, X = 0, Y = 0, Phase = 1, QuestionBaseId = frage.Id, QuestionBase = frage };

            using (var ctrlZellen = new GameGridCoordinatesController())
            using (var ctrlLink = new PlayerXGamesController(ctrlZellen))
            {
                await ctrlZellen.InsertAsync(zelle);
                await ctrlLink.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = anna.Id });
                await ctrlLink.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = ben.Id });
                await ctrlZellen.SaveChangesAsync();
            }

            spiel.GameGridCoordinates.Add(zelle);
            spiel.PlayerXGames.Add(new PlayerXGame { GameId = spiel.Id, PlayerId = anna.Id, Player = anna });
            spiel.PlayerXGames.Add(new PlayerXGame { GameId = spiel.Id, PlayerId = ben.Id, Player = ben });
        }

        /// <summary>So wie das Ergebnisfenster: Zeilen beim Öffnen anlegen, dann werten.</summary>
        private async Task SpieleAsync(int punkteAnna, int minusBen)
        {
            using var ctrl = new QuestionResultsController();
            var zeilen = new List<QuestionResult>();

            foreach (var (spieler, plus, minus) in new[] { (anna, punkteAnna, 0), (ben, 0, minusBen) })
            {
                var zeile = zelle.QuestionResults.FirstOrDefault(r => r.PlayerId == spieler.Id)
                    ?? new QuestionResult { Id = Guid.NewGuid(), PlayerId = spieler.Id, GameId = spiel.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = zelle.QuestionBaseId!.Value };

                zeile.Score = plus;
                zeile.CorrectAnswered = plus > 0;
                zeile.RightCount = plus > 0 ? 1 : 0;
                zeile.MinusScore = minus;
                zeile.WrongCount = minus > 0 ? 1 : 0;
                await ctrl.UpsertAsync(zeile);
                zeilen.Add(zeile);
            }

            await ctrl.SaveChangesAsync();

            // Das Ergebnisfenster ersetzt die Liste durch frische Objekte.
            zelle.QuestionResults = zeilen.Select(z => z.CloneWithoutReferences()).ToList();
            zelle.IsDone = true;
            spiel.CurrentChoosingPlayerId = anna.Id == spiel.CurrentChoosingPlayerId ? ben.Id : anna.Id;
        }

        private async Task<List<QuestionResult>> AusDerDatenbankAsync()
        {
            using var ctrl = new QuestionResultsController();
            return await ctrl.GetAllResultsForCoordinate(zelle.Id) ?? new();
        }

        [TestMethod]
        public async Task TheFirstScoringOfACellIsUndoneCompletely()
        {
            var merk = LetzteWertung.Merke(spiel, zelle);

            await SpieleAsync(punkteAnna: 200, minusBen: 100);

            Assert.IsTrue(merk.HatSichGeaendert(spiel));
            CollectionAssert.AreEquivalent(
                new[] { (anna.Id, 200), (ben.Id, -100) },
                merk.Aenderungen().ToList());

            var text = merk.Beschreibung(spiel);
            StringAssert.Contains(text, "Hauptstadt");
            StringAssert.Contains(text, "Anna: −200");
            StringAssert.Contains(text, "Ben: +100");
            StringAssert.Contains(text, "wieder offen");

            await merk.NimmZurueckAsync(spiel);

            Assert.IsFalse(zelle.IsDone);
            Assert.AreEqual(anna.Id, spiel.CurrentChoosingPlayerId);
            Assert.IsTrue(zelle.QuestionResults.All(r => r.FinalScore == 0 && !r.CorrectAnswered && r.RightCount == 0 && r.WrongCount == 0));
            Assert.IsTrue((await AusDerDatenbankAsync()).All(r => r.FinalScore == 0 && !r.CorrectAnswered), "In der Datenbank stehen noch Punkte.");
        }

        /// <summary>Eine nachträgliche Korrektur an einer schon gespielten Zelle geht auf deren vorigen Stand zurück, nicht auf null.</summary>
        [TestMethod]
        public async Task ACorrectionReturnsToThePreviousScoreNotToZero()
        {
            await SpieleAsync(punkteAnna: 200, minusBen: 0);

            var merk = LetzteWertung.Merke(spiel, zelle);
            await SpieleAsync(punkteAnna: 400, minusBen: 100);

            await merk.NimmZurueckAsync(spiel);

            var anna_ = (await AusDerDatenbankAsync()).Single(r => r.PlayerId == anna.Id);
            Assert.AreEqual(200, anna_.FinalScore);
            Assert.IsTrue(zelle.IsDone, "Die Zelle war schon vorher erledigt.");
        }

        [TestMethod]
        public async Task OnlyLookingAtACellChangesNothing()
        {
            await SpieleAsync(punkteAnna: 200, minusBen: 0);

            var merk = LetzteWertung.Merke(spiel, zelle);

            Assert.IsFalse(merk.HatSichGeaendert(spiel));
            Assert.AreEqual(0, merk.Aenderungen().Count);
        }
    }
}
