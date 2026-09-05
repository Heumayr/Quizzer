using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Eine Zelle, deren Ergebniszeilen nicht mehr zur Mannschaft passen.
    /// <para>
    /// Das entsteht, sobald waehrend eines Spiels ein Spieler herausgenommen wird, nachdem schon
    /// eine Zelle gespielt war - die Ergebniszeilen bleiben, der Eintrag in der Mannschaft geht.
    /// Bis 2026-09-06 warf das Oeffnen der Zelle dann "Invalid results count", und die Zelle war
    /// dauerhaft nicht mehr spielbar. In der Spieldatenbank des Nutzers lag genau so ein Fall.
    /// </para>
    /// </summary>
    [TestClass]
    public class ResultRosterMismatchUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 2, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Legt eine Ergebniszeile fuer einen Spieler an, der nicht zur Mannschaft gehoert.</summary>
        private async Task<Guid> AddResultOfAStrangerAsync()
        {
            var fremder = new Player
            {
                Id = Guid.NewGuid(),
                Designation = "Ehemaliger",
                DisplayName = "Ehemaliger",
            };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(fremder);
                await ctrl.SaveChangesAsync();
            }

            var ergebnisId = Guid.NewGuid();

            using (var ctrl = new QuestionResultsController())
            {
                await ctrl.InsertAsync(new QuestionResult
                {
                    Id = ergebnisId,
                    PlayerId = fremder.Id,
                    GameId = world.Game.Id,
                    GameGridCoordinateId = world.Coordinate.Id,
                    QuestionBaseId = world.Question.Id,
                    Score = 500,
                });

                await ctrl.SaveChangesAsync();
            }

            return ergebnisId;
        }

        /// <summary>
        /// Der gemeldete Fall: die Zelle muss sich oeffnen lassen, statt mit einer Ausnahme
        /// stehenzubleiben.
        /// </summary>
        [TestMethod]
        public async Task ACellWithResultsOfFormerPlayersStillOpens()
        {
            await AddResultOfAStrangerAsync();

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            await vm.LoadForTestAsync();

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.IsNotNull(vm.PlayersResultViewModel,
                "Ohne Ergebnisansicht kann der Spielleiter keine Punkte vergeben.");

            Assert.AreEqual(world.Players.Count, vm.PlayersResultViewModel!.PlayerResultContextList.Count,
                "Es muss je Mitspieler genau eine Karte geben.");
        }

        /// <summary>
        /// Und die Zeile des Ehemaligen ist danach weg - sonst waechst die Zelle bei jedem
        /// Oeffnen weiter und die Punktetafel zaehlt Fremdes mit.
        /// </summary>
        [TestMethod]
        public async Task TheStrangersResultIsRemoved()
        {
            var ergebnisId = await AddResultOfAStrangerAsync();

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            using var ctrl = new QuestionResultsController();
            var geblieben = await ctrl.GetAsync(ergebnisId);

            Assert.IsNull(geblieben,
                "Die Ergebniszeile eines Spielers ausserhalb der Mannschaft steht noch da.");
        }

        /// <summary>
        /// Die wichtigere Haelfte: die Ergebnisse der aktuellen Mitspieler bleiben unberuehrt.
        /// Ein Abgleich, der zu viel raeumt, kostet die Punkte einer gespielten Runde.
        /// </summary>
        [TestMethod]
        public async Task TheResultsOfActualPlayersAreKept()
        {
            var spieler = world.Players[0];
            var eigenesErgebnis = Guid.NewGuid();

            using (var ctrl = new QuestionResultsController())
            {
                await ctrl.InsertAsync(new QuestionResult
                {
                    Id = eigenesErgebnis,
                    PlayerId = spieler.Id,
                    GameId = world.Game.Id,
                    GameGridCoordinateId = world.Coordinate.Id,
                    QuestionBaseId = world.Question.Id,
                    Score = 300,
                });

                await ctrl.SaveChangesAsync();
            }

            await AddResultOfAStrangerAsync();

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            using var pruefer = new QuestionResultsController();
            var geblieben = await pruefer.GetAsync(eigenesErgebnis);

            Assert.IsNotNull(geblieben,
                "Das Ergebnis eines Mitspielers wurde mitgeraeumt - die Punkte der Runde waeren weg.");

            Assert.AreEqual(300, geblieben!.Score, "Der Punktestand hat sich veraendert.");
        }
    }
}
