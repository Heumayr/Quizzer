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

        /// <summary>
        /// P5-1, wie R1-4 im Einzelzeilen-Rückfall: der Laptop bucht ein Ergebnis an Rot und setzt sich ins zweite Spiel, der PC
        /// löscht das erste Spiel samt Rot, ohne das Ergebnis zu kennen, und setzt sich ebenfalls ins zweite - der doppelte Platz
        /// lässt den Gesamtpush scheitern. Einzeln gehen Zelle und Rot vor dem Spiel und werden abgewiesen, danach nimmt das Spiel
        /// beide mit: keine Zeile steht zugleich in Rejected und Deleted, offen bleibt nur der doppelte Platz.
        /// </summary>
        [TestMethod]
        public async Task ATeamGameDeletedRowByRowLeavesNoPhantomRejection()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = set.Game.Id, Name = "Rot", ColorIndex = 5 };
            var zweites = new Game { Id = Guid.NewGuid(), Designation = "Zweites Spiel", ModeratorPlayerId = me };

            await using (var local = pc.Local())
            {
                local.AddRange(rot, zweites);
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await pc.Sync.PushAsync()).Rejected.Count);
            await laptop.Sync.PullAsync();

            await using (var local = laptop.Local())
            {
                local.AddRange(
                    new QuestionResult
                    {
                        Id = Guid.NewGuid(), GameId = set.Game.Id, GameGridCoordinateId = set.Cell.Id, QuestionBaseId = set.Question.Id,
                        PlayerId = me, Score = 100, GameTeamId = rot.Id,
                    },
                    new PlayerXGame { Id = Guid.NewGuid(), GameId = zweites.Id, PlayerId = me });
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await laptop.Sync.PushAsync()).Rejected.Count);
            var doppelt = new PlayerXGame { Id = Guid.NewGuid(), GameId = zweites.Id, PlayerId = me };

            await using (var local = pc.Local())
            {
                local.PlayerXGames.RemoveRange(local.PlayerXGames.Where(x => x.GameId == set.Game.Id));
                local.GameGridCoordinates.RemoveRange(local.GameGridCoordinates.Where(c => c.GameId == set.Game.Id));
                local.GameTeams.Remove(await local.GameTeams.SingleAsync(t => t.Id == rot.Id));
                local.Games.Remove(await local.Games.SingleAsync(g => g.Id == set.Game.Id));
                local.PlayerXGames.Add(doppelt);
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();

            Assert.IsTrue(pushed.Rejected.Any(r => r.Id == doppelt.Id && r.Reason == SyncRejectReason.Invalid),
                "Der doppelte Platz kam nicht als gescheiterte Einzelzeile zurück - der Rückfall lief gar nicht.");
            var phantom = pushed.Rejected.Where(r => r.Id == rot.Id || r.Id == set.Cell.Id).Select(r => $"{r.Type} {r.Reason} {r.Message}").ToList();
            Assert.AreEqual(0, phantom.Count, "Phantom-Abweisung: " + string.Join("; ", phantom));
            Assert.AreEqual(doppelt.Id, (await pc.Sync.GetRejectionsAsync()).Single().Id, "Unter den offenen Abweisungen steht mehr als der doppelte Platz.");
        }
    }
}
