using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>Ein Team mit Ergebnissen als Wettlauf zweier Geräte (M37 P5, R1-1).</summary>
    public sealed partial class ClientSyncTests
    {
        /// <summary>
        /// Der Laptop ändert ein Ergebnis an Rot, der PC löscht es auf altem Stand und danach Rot. Das Ergebnis kommt als
        /// Konflikt zurück und bleibt - also bleibt auch Rot, und das Ergebnis behält seinen Stempel (E1: der Teamstand ändert
        /// sich nicht ohne die Spielleitung).
        /// </summary>
        [TestMethod]
        public async Task ATeamWhoseResultChangedElsewhereIsNotDeleted()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = set.Game.Id, Name = "Rot", ColorIndex = 5 };
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = set.Game.Id, GameGridCoordinateId = set.Cell.Id, QuestionBaseId = set.Question.Id,
                PlayerId = me, Score = 100, GameTeamId = rot.Id,
            };

            await using (var local = pc.Local())
            {
                local.AddRange(rot, ergebnis);
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await pc.Sync.PushAsync()).Rejected.Count);
            await laptop.Sync.PullAsync();

            await using (var local = laptop.Local())
            {
                (await local.QuestionResults.SingleAsync(r => r.Id == ergebnis.Id)).Score = 300;
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await laptop.Sync.PushAsync()).Rejected.Count);

            await using (var local = pc.Local())
            {
                local.QuestionResults.Remove(await local.QuestionResults.SingleAsync(r => r.Id == ergebnis.Id));
                local.GameTeams.Remove(await local.GameTeams.SingleAsync(t => t.Id == rot.Id));
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();

            Assert.IsTrue(pushed.Rejected.Any(r => r.Id == ergebnis.Id && r.Reason == SyncRejectReason.Conflict), "Das geänderte Ergebnis wurde gelöscht.");
            Assert.IsTrue(pushed.Rejected.Any(r => r.Id == rot.Id && r.Reason == SyncRejectReason.Forbidden), "Rot ging trotz seines Ergebnisses.");

            await laptop.Sync.PullAsync();
            await using var laptopDb = laptop.Local();
            Assert.IsTrue(await laptopDb.GameTeams.AnyAsync(t => t.Id == rot.Id), "Am Server steht Rot nicht mehr.");
            Assert.AreEqual((300, (Guid?)rot.Id), await laptopDb.QuestionResults.Where(r => r.Id == ergebnis.Id).Select(r => new ValueTuple<int, Guid?>(r.Score, r.GameTeamId)).SingleAsync(),
                "Das Ergebnis hat seinen Stand oder seinen Stempel verloren.");
        }
    }
}
