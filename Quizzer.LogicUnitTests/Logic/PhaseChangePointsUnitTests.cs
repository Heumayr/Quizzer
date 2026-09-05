using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Phasenwechsel mitten im Spielabend, an einem aus der Datenbank geladenen Spiel.
    /// <para>
    /// <c>SetPhase</c> rechnet die Punkte einer Zelle neu - aber
    /// <c>CalculateAndSetCurrentPoints</c> steigt aus, wenn die Rueckverweise auf Frage und
    /// Spiel fehlen, und behaelt dann den alten Stand. Ob die Include-Kette von
    /// <c>GamesController</c> beides mitbringt, war nie gemessen. Faellt sie aus, sind die
    /// Punkte der zweiten Spielhaelfte still die der ersten.
    /// </para>
    /// </summary>
    [TestClass]
    public class PhaseChangePointsUnitTests
    {
        private Category kategorie = null!;
        private QuestionBase frage = null!;
        private Game spiel = null!;
        private GameGridCoordinate offen = null!;
        private GameGridCoordinate gespielt = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            kategorie = new Category { Id = Guid.NewGuid(), Designation = "Berge" };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Glockner",
                DesignationShort = "G",
                CategoryId = kategorie.Id,
                Points = 100,
                MinusPoints = 50,
                Difficulty = Difficulty.Level1,
            };

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = "Phasenspiel",
                Phase = 1,
                SuggestedPhases = 2,
                PhaseMultiplier = 1,
                PhaseAddition = 0,
            };

            offen = NeueZelle(0, false);
            gespielt = NeueZelle(1, true);

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(offen);
                await ctrl.InsertAsync(gespielt);
                await ctrl.SaveChangesAsync();
            }
        }

        private GameGridCoordinate NeueZelle(int x, bool erledigt)
        {
            var zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                Game = spiel,
                X = x,
                Y = 0,
                Phase = 1,
                QuestionBaseId = frage.Id,
                QuestionBase = frage,
            };

            zelle.CalculateAndSetCurrentPoints();
            zelle.IsDone = erledigt;

            return zelle;
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.DeleteAsync(frage.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(kategorie.Id);
                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>
        /// Ein geladenes Spiel bringt die Rueckverweise mit, die die Punkterechnung braucht.
        /// Ohne sie steigt sie aus und behaelt den alten Stand - lautlos.
        /// </summary>
        [TestMethod]
        public async Task ALoadedGameCarriesWhatThePointCalculationNeeds()
        {
            using var ctrl = new GamesController();

            var geladen = await ctrl.GetAsync(spiel.Id);

            Assert.IsNotNull(geladen, "Das Spiel wurde nicht gefunden.");

            foreach (var zelle in geladen!.GameGridCoordinates)
            {
                Assert.IsNotNull(zelle.QuestionBase,
                    "Die Include-Kette laedt die Frage nicht mit - die Punkterechnung stiege aus.");

                Assert.IsNotNull(zelle.Game,
                    "Der Rueckverweis auf das Spiel fehlt - die Punkterechnung stiege ebenfalls aus.");
            }
        }

        /// <summary>
        /// Der Phasenwechsel hebt die Punkte der offenen Zellen und laesst die gespielte in Ruhe.
        /// </summary>
        [TestMethod]
        public async Task RaisingThePhaseLiftsOpenCellsAndLeavesPlayedOnesAlone()
        {
            int punkteOffenVorher;
            int punkteGespieltVorher;

            using (var ctrl = new GamesController())
            {
                var geladen = await ctrl.GetAsync(spiel.Id);

                punkteOffenVorher = geladen!.GameGridCoordinates.Single(c => !c.IsDone).CurrentPoints;
                punkteGespieltVorher = geladen.GameGridCoordinates.Single(c => c.IsDone).CurrentPoints;

                Assert.IsTrue(punkteOffenVorher > 0,
                    "Die offene Zelle ist nichts wert - dann misst der Vergleich nichts.");

                geladen.RaisePhase();

                Assert.AreEqual(2, geladen.Phase);

                using var ctrlZellen = new GameGridCoordinatesController(ctrl);

                await ctrlZellen.UpsertAsync(geladen.GameGridCoordinates);
                await ctrl.UpdateAsync(geladen);

                // Eine Sicherung fuer beide Controller - sie teilen den DataContext.
                await ctrl.SaveChangesAsync();
            }

            using var pruefer = new GamesController();

            var danach = await pruefer.GetAsync(spiel.Id);

            var offenDanach = danach!.GameGridCoordinates.Single(c => !c.IsDone);
            var gespieltDanach = danach.GameGridCoordinates.Single(c => c.IsDone);

            Assert.AreEqual(2, offenDanach.Phase,
                "Die offene Zelle steht noch in der alten Phase.");

            Assert.AreEqual(punkteOffenVorher * 2, offenDanach.CurrentPoints,
                "Die Punkte der offenen Zelle wurden nicht auf die neue Phase gerechnet - "
                + "die zweite Spielhaelfte laeuft dann mit den Werten der ersten.");

            Assert.AreEqual(punkteGespieltVorher, gespieltDanach.CurrentPoints,
                "Die gespielte Zelle hat ihre Punkte geaendert - vergebene Punkte muessen "
                + "einfrieren.");

            Assert.AreEqual(1, gespieltDanach.Phase,
                "Die gespielte Zelle wurde in die neue Phase gezogen.");
        }
    }
}
