using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Server.Tests
{
    /// <summary>Teams von Ende zu Ende (M37 T1): PC und Laptop mit eigener Datenbank, derselbe Abgleich wie im Programm.</summary>
    public sealed partial class ClientSyncTests
    {
        /// <summary>
        /// Team samt Zuteilung in einem Push: am PC in einem Speichern angelegt, zugeteilt und eingestellt - am Laptop kommt
        /// alles an. Der Rang trägt das: der Platz zeigt auf das Team und wird nach ihm eingespielt.
        /// </summary>
        [TestMethod]
        public async Task ATeamAndItsAssignmentTravelInOnePush()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = SyncTestData.NewGame("Teamabend");
            set.Game.ModeratorPlayerId = me;
            set.Question.OwnerPlayerId = me;
            set.Game.IsTeamGame = true;
            set.Game.TeamMinigameScoring = TeamMinigameScoring.Sum;
            set.Game.TeamCouncilSeconds = 45;
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = set.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 1 };
            var platz = new PlayerXGame { Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = me, GameTeamId = rot.Id };

            await using (var local = pc.Local())
            {
                local.AddRange(set.Category, set.Question, set.Game, set.Cell, rot, platz);
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(0, pushed.Rejected.Count, string.Join("; ", pushed.Rejected.Select(r => r.Message)));

            await laptop.Sync.PullAsync();
            await using var laptopDb = laptop.Local();
            var team = await laptopDb.GameTeams.SingleAsync(t => t.Id == rot.Id);
            Assert.AreEqual(("Rot", 5, 1), (team.Name, team.ColorIndex, team.SortOrder));
            Assert.AreEqual(rot.Id, (await laptopDb.PlayerXGames.SingleAsync(x => x.Id == platz.Id)).GameTeamId, "Die Zuteilung kam nicht an.");
            var spiel = await laptopDb.Games.SingleAsync(g => g.Id == set.Game.Id);
            Assert.AreEqual((true, TeamMinigameScoring.Sum, 45), (spiel.IsTeamGame, spiel.TeamMinigameScoring, spiel.TeamCouncilSeconds));
        }

        /// <summary>
        /// Team löschen samt Umsetzen: am Laptop Rot gelöscht und die Mitglieder zu Blau - beides in einem Push. Der Server
        /// und danach der PC spielen Löschungen vor den Zeilen ein; ohne das Freigeben scheiterte das am NO ACTION des Platzes.
        /// </summary>
        [TestMethod]
        public async Task DeletingATeamAndMovingItsMembersTravelsInOnePush()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = set.Game.Id, Name = "Rot", ColorIndex = 5 };
            var blau = new GameTeam { Id = Guid.NewGuid(), GameId = set.Game.Id, Name = "Blau", ColorIndex = 4 };

            await using (var local = pc.Local())
            {
                local.AddRange(rot, blau);
                await local.SaveChangesAsync();
                (await local.PlayerXGames.SingleAsync(x => x.GameId == set.Game.Id)).GameTeamId = rot.Id;
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await pc.Sync.PushAsync()).Rejected.Count);
            await laptop.Sync.PullAsync();

            await using (var local = laptop.Local())
            {
                (await local.PlayerXGames.SingleAsync(x => x.GameId == set.Game.Id)).GameTeamId = blau.Id;
                local.GameTeams.Remove(await local.GameTeams.SingleAsync(t => t.Id == rot.Id));
                await local.SaveChangesAsync();
            }

            var pushed = await laptop.Sync.PushAsync();
            Assert.AreEqual(0, pushed.Rejected.Count, string.Join("; ", pushed.Rejected.Select(r => r.Message)));
            Assert.AreEqual(1, pushed.Deleted, "Das Löschen des Teams kam nicht am Server an.");

            await pc.Sync.PullAsync();
            await using var pcDb = pc.Local();
            Assert.IsFalse(await pcDb.GameTeams.AnyAsync(t => t.Id == rot.Id), "Am PC steht Rot noch.");
            Assert.AreEqual(blau.Id, (await pcDb.PlayerXGames.SingleAsync(x => x.GameId == set.Game.Id)).GameTeamId, "Am PC ist der Platz nicht bei Blau.");
        }
    }
}
