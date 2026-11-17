using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Context;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Teams eines Spiels (M37 T1): laden mit dem Spiel, löschen samt Herausnehmen der Mitglieder, und ein Team mit
    /// Ergebnissen bleibt stehen. Alle drei Fremdschlüssel stehen auf NO ACTION - jeder Riegel hier deckt einen.
    /// </summary>
    [TestClass]
    public class GameTeamsControllerUnitTests
    {
        private Category kategorie = null!;
        private QuestionBase frage = null!;
        private Player anna = null!;
        private Game spiel = null!;
        private GameGridCoordinate zelle = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private PlayerXGame annasPlatz = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            kategorie = new Category { Id = Guid.NewGuid(), Designation = "Teams" };
            frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Teamfrage", DesignationShort = "T", CategoryId = kategorie.Id, Points = 100 };
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
            spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamspiel", Phase = 1, SuggestedPhases = 1, IsTeamGame = true };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1, QuestionBaseId = frage.Id };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5, SortOrder = 1 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau", ColorIndex = 4, SortOrder = 0 };
            annasPlatz = new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = anna.Id, GameTeamId = rot.Id };

            using (var ctrl = new CategoriesController())
            using (var ctrlFrage = new QuestionBasesController(ctrl))
            {
                await ctrl.InsertAsync(kategorie);
                await ctrlFrage.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            using var ctrlSpieler = new PlayersController();
            using var ctrlSpiel = new GamesController(ctrlSpieler);
            using var ctrlZelle = new GameGridCoordinatesController(ctrlSpieler);
            using var ctrlTeams = new GameTeamsController(ctrlSpieler);
            using var ctrlPlatz = new PlayerXGamesController(ctrlSpieler);

            await ctrlSpieler.InsertAsync(anna);
            await ctrlSpiel.InsertAsync(spiel);
            await ctrlZelle.InsertAsync(zelle);
            await ctrlTeams.InsertAsync(rot);
            await ctrlTeams.InsertAsync(blau);
            await ctrlPlatz.InsertAsync(annasPlatz);
            await ctrlSpieler.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                if (await ctrl.GetAsync(spiel.Id) != null)
                {
                    await ctrl.DeleteAsync(spiel.Id);
                    await ctrl.SaveChangesAsync();
                }
            }

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.DeleteAsync(frage.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                await ctrl.DeleteAsync(anna.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(kategorie.Id);
                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        private static async Task<Guid?> TeamVonAsync(Guid platzId)
        {
            await using var context = new DataContext();
            return await context.PlayerXGames.Where(x => x.Id == platzId).Select(x => x.GameTeamId).SingleAsync();
        }

        private async Task StempleErgebnisAsync(Guid teamId)
        {
            using var ctrl = new QuestionResultsController();
            await ctrl.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(), PlayerId = anna.Id, GameId = spiel.Id, GameGridCoordinateId = zelle.Id,
                QuestionBaseId = frage.Id, Score = 100, GameTeamId = teamId,
            });
            await ctrl.SaveChangesAsync();
        }

        /// <summary>Das Spiel bringt seine Teams mit - ohne das <c>Include</c> stünde ein Teamspiel ohne Teams da.</summary>
        [TestMethod]
        public async Task TheGameLoadsItsTeams()
        {
            using var ctrl = new GamesController();
            var geladen = await ctrl.GetAsync(spiel.Id);

            CollectionAssert.AreEquivalent(new[] { rot.Id, blau.Id }, geladen!.GameTeams.Select(t => t.Id).ToList());
        }

        [TestMethod]
        public async Task TheTeamsOfAGameComeInTheirOrder()
        {
            using var ctrl = new GameTeamsController();

            CollectionAssert.AreEqual(new[] { "Blau", "Rot" }, (await ctrl.GetForGameAsync(spiel.Id)).Select(t => t.Name).ToList());
        }

        /// <summary>Löschen nimmt die Mitglieder heraus; der Platz bleibt, nur ohne Team.</summary>
        [TestMethod]
        public async Task DeletingATeamReleasesItsMembers()
        {
            using (var ctrl = new GameTeamsController())
            {
                Assert.IsTrue(await ctrl.DeleteAsync(rot.Id));
                await ctrl.SaveChangesAsync();
            }

            Assert.IsNull(await TeamVonAsync(annasPlatz.Id), "Anna hängt noch am gelöschten Team.");

            using var teams = new GameTeamsController();
            CollectionAssert.AreEqual(new[] { blau.Id }, (await teams.GetForGameAsync(spiel.Id)).Select(t => t.Id).ToList());
        }

        /// <summary>
        /// V05-Muster: scheitert das Speichern des Löschens, steht Anna wieder im Team. Die Störung ist ein Team zu
        /// einem Spiel, das es nicht gibt - sein Fremdschlüssel scheitert im selben Speichern.
        /// </summary>
        [TestMethod]
        public async Task AFailedDeleteKeepsTheMembersInTheTeam()
        {
            using (var ctrl = new GameTeamsController())
            {
                await ctrl.DeleteAsync(rot.Id);
                await ctrl.InsertAsync(new GameTeam { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Name = "Störung" });

                await Assert.ThrowsExactlyAsync<DbUpdateException>(async () => await ctrl.SaveChangesAsync(),
                    "Die Störung hätte das Speichern scheitern lassen müssen - sonst misst dieser Test nichts.");
            }

            TestDatabase.ClearDiscardedChanges();

            Assert.AreEqual(rot.Id, await TeamVonAsync(annasPlatz.Id), "Das Löschen scheiterte, und Anna ist trotzdem aus dem Team.");
        }

        /// <summary>Ein Team mit Ergebnissen lässt sich nur umbenennen: Löschen wird abgelehnt, bevor etwas geschieht.</summary>
        [TestMethod]
        public async Task ATeamWithResultsCannotBeDeletedButRenamed()
        {
            await StempleErgebnisAsync(rot.Id);

            using (var ctrl = new GameTeamsController())
            {
                var ex = await Assert.ThrowsExactlyAsync<TeamHasResultsException>(async () => await ctrl.DeleteAsync(rot.Id));
                Assert.AreEqual(1, ex.Results);
            }

            Assert.AreEqual(rot.Id, await TeamVonAsync(annasPlatz.Id), "Der abgelehnte Löschversuch nahm Anna trotzdem heraus.");

            using (var ctrl = new GameTeamsController())
            {
                rot.Name = "Feuer";
                await ctrl.UpdateAsync(rot);
                await ctrl.SaveChangesAsync();
            }

            using var teams = new GameTeamsController();
            Assert.AreEqual("Feuer", (await teams.GetForGameAsync(spiel.Id)).Single(t => t.Id == rot.Id).Name);
        }

        /// <summary>Nur das gestempelte Team ist gesperrt - ein Ergebnis von Rot hält Blau nicht fest.</summary>
        [TestMethod]
        public async Task AnotherTeamsResultsDoNotBlockTheDelete()
        {
            await StempleErgebnisAsync(rot.Id);

            using var ctrl = new GameTeamsController();
            Assert.IsTrue(await ctrl.DeleteAsync(blau.Id));
            await ctrl.SaveChangesAsync();
        }

        [TestMethod]
        [DataRow("", 0, 0, DisplayName = "leerer Name")]
        [DataRow("   ", 0, 0, DisplayName = "nur Leerzeichen")]
        [DataRow("123456789012345678901234567890X", 0, 0, DisplayName = "31 Zeichen")]
        [DataRow("Rot", -1, 0, DisplayName = "Farbe unter 0")]
        [DataRow("Rot", 8, 0, DisplayName = "Farbe über 7")]
        [DataRow("Rot", 0, -1, DisplayName = "Sprecher negativ")]
        public async Task AnInvalidTeamIsRefusedBeforeTheDatabase(string name, int farbe, int sprecher)
        {
            using var ctrl = new GameTeamsController();

            await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
                await ctrl.InsertAsync(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = name, ColorIndex = farbe, SpeakerTurn = sprecher }));
        }

        /// <summary>Die Grenzwerte selbst gehen durch - 30 Zeichen, Farbe 0 und 7.</summary>
        [TestMethod]
        public async Task TheLimitsThemselvesAreAccepted()
        {
            using var ctrl = new GameTeamsController();
            await ctrl.InsertAsync(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = new string('A', GameTeam.MaxNameLength), ColorIndex = 7 });
            await ctrl.InsertAsync(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Null", ColorIndex = 0 });
            await ctrl.SaveChangesAsync();
        }

        /// <summary>Die Spalte hält dieselbe Grenze wie die Prüfung - sonst scheiterte erst die Datenbank.</summary>
        [TestMethod]
        public void TheNameColumnHasTheSameLimit()
        {
            using var context = new DataContext();
            var spalte = context.Model.FindEntityType(typeof(GameTeam))!.FindProperty(nameof(GameTeam.Name))!;

            Assert.AreEqual(GameTeam.MaxNameLength, spalte.GetMaxLength());
        }
    }
}
