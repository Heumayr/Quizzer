using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Die Aufstellung eines Spiels in einem Zug (M37 T2): Teams anlegen, ändern, entfernen und zuteilen. Missbrauch
    /// (A01 fremdes Team, A06 Grenzen) und Ausnahmen (A10 Team mit Ergebnissen) lassen den Stand, wie er war.
    /// </summary>
    [TestClass]
    public class GameTeamsLineupUnitTests
    {
        private readonly List<Player> spieler = [];
        private Category kategorie = null!;
        private QuestionBase frage = null!;
        private Game spiel = null!;
        private Game anderes = null!;
        private GameTeam rot = null!;
        private GameTeam fremd = null!;

        private Player Anna => spieler[0];
        private Player Bert => spieler[1];
        private Player Carla => spieler[2];

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            spiel = new Game { Id = Guid.NewGuid(), Designation = "Aufstellung", Phase = 1, SuggestedPhases = 1, IsTeamGame = true };
            anderes = new Game { Id = Guid.NewGuid(), Designation = "Nebenan", Phase = 1, SuggestedPhases = 1, IsTeamGame = true };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0, SpeakerTurn = 2 };
            fremd = new GameTeam { Id = Guid.NewGuid(), GameId = anderes.Id, Name = "Fremd", ColorIndex = 1 };
            kategorie = new Category { Id = Guid.NewGuid(), Designation = "Aufstellung" };
            frage = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Frage", DesignationShort = "A", Points = 100, CategoryId = kategorie.Id };

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

            using (var ctrl = new PlayersController())
            {
                foreach (var name in new[] { "Anna", "Bert", "Carla" })
                {
                    var p = new Player { Id = Guid.NewGuid(), Designation = name, DisplayName = name };
                    spieler.Add(p);
                    await ctrl.InsertAsync(p);
                }

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.UpsertAsync(anderes);
                await ctrl.SaveChangesAsync();
            }

            using var ctrlTeams = new GameTeamsController();
            using var ctrlPlatz = new PlayerXGamesController(ctrlTeams);

            await ctrlTeams.InsertAsync(rot);
            await ctrlTeams.InsertAsync(fremd);

            foreach (var p in spieler)
                await ctrlPlatz.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = p.Id, GameTeamId = p == Anna ? rot.Id : null });

            await ctrlTeams.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new GamesController())
            {
                await ctrl.DeleteAsync(spiel.Id);
                await ctrl.DeleteAsync(anderes.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.DeleteAsync(frage.Id);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new PlayersController())
            {
                foreach (var p in spieler)
                    await ctrl.DeleteAsync(p.Id);

                await ctrl.SaveChangesAsync();
            }

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(kategorie.Id);
                await ctrl.SaveChangesAsync();
            }

            TestDatabase.ClearDiscardedChanges();
        }

        private static GameTeam Entwurf(GameTeam team) =>
            new() { Id = team.Id, Name = team.Name, ColorIndex = team.ColorIndex, SortOrder = team.SortOrder };

        private GameTeam Neu(string name, int farbe, int platz) => new() { Id = Guid.NewGuid(), GameId = spiel.Id, Name = name, ColorIndex = farbe, SortOrder = platz };

        /// <summary>Teams in der Reihenfolge und die Zuteilung je Spielername - aus der Datenbank.</summary>
        private async Task<(string Teams, string Zuteilung)> StandAsync()
        {
            using var teams = new GameTeamsController();
            var liste = await teams.GetForGameAsync(spiel.Id);

            await using var context = new Quizzer.Logic.Context.DataContext();
            var plaetze = context.PlayerXGames.Where(x => x.GameId == spiel.Id).ToList();

            return (
                string.Join(", ", liste.Select(t => $"{t.Name}/{t.ColorIndex}/{t.SpeakerTurn}")),
                string.Join(", ", spieler.Select(p => $"{p.Designation}:{liste.FirstOrDefault(t => t.Id == plaetze.Single(x => x.PlayerId == p.Id).GameTeamId)?.Name ?? "-"}")));
        }

        [TestMethod]
        public async Task ALineupIsWrittenInOneGoAndKeepsTheSpeaker()
        {
            var rote = Entwurf(rot);
            rote.Name = "Rote Füchse";
            rote.ColorIndex = 6;
            var blau = Neu("Blau", 4, 1);

            using var ctrl = new GameTeamsController();
            var frisch = await ctrl.SaveLineupAsync(spiel.Id, [rote, blau], new Dictionary<Guid, Guid?> { [Bert.Id] = blau.Id, [Carla.Id] = blau.Id });

            Assert.AreEqual(("Rote Füchse/6/2, Blau/4/0", "Anna:Rote Füchse, Bert:Blau, Carla:Blau"), await StandAsync());
            Assert.AreEqual(2, frisch.Teams.Count);
            Assert.IsTrue(frisch.Teams.All(t => t.RowVersion != null) && frisch.Plaetze.All(x => x.RowVersion != null), "Der frische Stand kam ohne RowVersion zurück.");
        }

        [TestMethod]
        public async Task RemovingATeamReleasesItsMembers()
        {
            var blau = Neu("Blau", 4, 0);

            using var ctrl = new GameTeamsController();
            await ctrl.SaveLineupAsync(spiel.Id, [blau], new Dictionary<Guid, Guid?> { [Bert.Id] = blau.Id });

            Assert.AreEqual(("Blau/4/0", "Anna:-, Bert:Blau, Carla:-"), await StandAsync());
        }

        /// <summary>Wer im entfernten Team war, kann im selben Zug in ein anderes - das Freigeben stört das Zuteilen nicht.</summary>
        [TestMethod]
        public async Task AMemberOfARemovedTeamCanMoveInTheSameGo()
        {
            var blau = Neu("Blau", 4, 0);

            using var ctrl = new GameTeamsController();
            await ctrl.SaveLineupAsync(spiel.Id, [blau], new Dictionary<Guid, Guid?> { [Anna.Id] = blau.Id });

            Assert.AreEqual(("Blau/4/0", "Anna:Blau, Bert:-, Carla:-"), await StandAsync());
        }

        /// <summary>A10: ein Team mit Ergebnissen lässt sich nicht entfernen - es wird geworfen, nichts ist geschrieben.</summary>
        [TestMethod]
        public async Task ATeamWithResultsCannotBeRemovedAndNothingIsWritten()
        {
            await StempleAsync();
            var vorher = await StandAsync();
            var blau = Neu("Blau", 4, 0);

            using (var ctrl = new GameTeamsController())
            {
                await Assert.ThrowsExactlyAsync<TeamHasResultsException>(
                    () => ctrl.SaveLineupAsync(spiel.Id, [blau], new Dictionary<Guid, Guid?> { [Bert.Id] = blau.Id }));
            }

            TestDatabase.ClearDiscardedChanges();
            Assert.AreEqual(vorher, await StandAsync());
        }

        /// <summary>
        /// Scheitert es nach dem Entfernen (ein Spieler, der nicht am Spiel sitzt), ist auch das Freigeben der Mitglieder
        /// zurückgenommen - Anna steht wieder in Rot. Ohne Klammer wäre sie schon frei.
        /// </summary>
        [TestMethod]
        public async Task AFailureAfterTheRemovalLeavesTheMembersInPlace()
        {
            var vorher = await StandAsync();
            var blau = Neu("Blau", 4, 0);

            using (var ctrl = new GameTeamsController())
            {
                await Assert.ThrowsExactlyAsync<ArgumentException>(
                    () => ctrl.SaveLineupAsync(spiel.Id, [blau], new Dictionary<Guid, Guid?> { [Guid.NewGuid()] = blau.Id }));
            }

            TestDatabase.ClearDiscardedChanges();
            Assert.AreEqual(vorher, await StandAsync(), "Ein gescheitertes Speichern hat das entfernte Team schon geleert.");
        }

        /// <summary>A01: ein Team aus einem anderen Spiel - in der Zuteilung oder in der Liste - wird abgewiesen, gleiche Meldung.</summary>
        [TestMethod]
        public async Task ATeamOfAnotherGameIsRejected()
        {
            var vorher = await StandAsync();

            using var ctrl = new GameTeamsController();
            var zuteilung = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => ctrl.SaveLineupAsync(spiel.Id, [Entwurf(rot)], new Dictionary<Guid, Guid?> { [Bert.Id] = fremd.Id }));
            var liste = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => ctrl.SaveLineupAsync(spiel.Id, [Entwurf(rot), Entwurf(fremd)], new Dictionary<Guid, Guid?> { [Bert.Id] = fremd.Id }));

            StringAssert.StartsWith(zuteilung.Message, "Das Team gehört nicht zu diesem Spiel.");
            StringAssert.StartsWith(liste.Message, "Das Team gehört nicht zu diesem Spiel.");
            Assert.AreEqual(vorher, await StandAsync());

            using var teams = new GameTeamsController();
            Assert.AreEqual("Fremd", (await teams.GetForGameAsync(anderes.Id)).Single().Name, "Das fremde Team wurde angefasst.");
        }

        /// <summary>A06: Grenzen werden vor dem Schreiben geprüft - mit einem Satz statt eines rohen Datenbankfehlers.</summary>
        [TestMethod]
        [DataRow("", 4, DisplayName = "leerer Name")]
        [DataRow("123456789012345678901234567890X", 4, DisplayName = "31 Zeichen")]
        [DataRow("Blau", 8, DisplayName = "Farbe außerhalb")]
        [DataRow("Blau", 5, DisplayName = "Farbe doppelt")]
        [DataRow(" rot ", 4, DisplayName = "Name doppelt")]
        public async Task LimitsAreCheckedBeforeAnythingIsWritten(string name, int farbe)
        {
            var vorher = await StandAsync();
            var neu = Neu(name, farbe, 1);

            using var ctrl = new GameTeamsController();
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => ctrl.SaveLineupAsync(spiel.Id, [Entwurf(rot), neu], new Dictionary<Guid, Guid?> { [Bert.Id] = neu.Id }));

            Assert.AreEqual(vorher, await StandAsync());
        }

        /// <summary>A06: mehr Teams als Farben werden abgewiesen.</summary>
        [TestMethod]
        public async Task NineTeamsAreTooMany()
        {
            var neun = Enumerable.Range(0, 9).Select(i => Neu($"T{i}", i % 8, i)).ToList();

            using var ctrl = new GameTeamsController();
            var fehler = await Assert.ThrowsExactlyAsync<ArgumentException>(() => ctrl.SaveLineupAsync(spiel.Id, neun, new Dictionary<Guid, Guid?>()));

            StringAssert.Contains(fehler.Message, "Höchstens 8 Teams");
        }

        /// <summary>Ein Ergebnis mit Annas Stempel auf Rot.</summary>
        private async Task StempleAsync()
        {
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, Phase = 1, QuestionBaseId = frage.Id };

            using var ctrl = new GameGridCoordinatesController();
            using var ergebnisse = new QuestionResultsController(ctrl);

            await ctrl.InsertAsync(zelle);
            await ergebnisse.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = Anna.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = frage.Id, GameTeamId = rot.Id,
            });
            await ctrl.SaveChangesAsync();
        }
    }
}
