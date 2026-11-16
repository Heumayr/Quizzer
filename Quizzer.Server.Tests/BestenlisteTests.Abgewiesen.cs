using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// V07 (M37 D3): die Art einer Frage im selben Push zählt nur, wenn der Server diese Frage angenommen hat. Gemessen am
    /// 2026-11-16: eine abgewiesene Umdeutung zur Reaktion (Konflikt oder fremdes Konto) ließ einen Rohwert an einer Frage
    /// durch, die am Server Mehrheit blieb.
    /// </summary>
    public sealed partial class BestenlisteTests
    {
        private static async Task<string> ServerHashAsync(HttpClient client, Guid id)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!.Rows.Single(r => r.Id == id).Hash;

        private static void AssertRejected(SyncPushResponse result, Guid id, SyncRejectReason reason, string warum)
            => Assert.IsTrue(result.Rejected.Any(r => r.Id == id && r.Reason == reason),
                warum + " " + string.Join("; ", result.Rejected.Select(r => $"{r.Type}: {r.Reason}")));

        /// <summary>Dasselbe Konto deutet die Mehrheit mit veraltetem Stand zur Reaktion um - Konflikt, die Frage bleibt Mehrheit.</summary>
        [TestMethod]
        public async Task AKindFromAConflictingQuestionDoesNotCount()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var umgedeutet = Minispiel(MinigameKind.Reaction, abend.Spiel.Category.Id);
            umgedeutet.Id = abend.Mehrheit[0].QuestionBaseId!.Value;
            var ergebnis = Ergebnis(abend, abend.Mehrheit[0], 250);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(umgedeutet, "veraltet"), SyncTestData.New(ergebnis)],
            });

            AssertRejected(result, umgedeutet.Id, SyncRejectReason.Conflict, "Die Umdeutung ging durch - sonst misst dieser Test nichts.");
            AssertInvalid(result, ergebnis, "Der Rohwert kam über die Art einer abgewiesenen Frage durch.");
        }

        /// <summary>
        /// Die Gegenrichtung: eine erlaubte Umdeutung im selben Push zählt. Würde die Art vom Server vorgezogen, wäre das
        /// Ergebnis ungültig, obwohl die Frage danach eine Reaktion ist.
        /// </summary>
        [TestMethod]
        public async Task ALegitimateKindChangeInTheSamePushCounts()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory, "Umdeutung");

            var umgedeutet = Minispiel(MinigameKind.Reaction, abend.Spiel.Category.Id);
            umgedeutet.Id = abend.Mehrheit[0].QuestionBaseId!.Value;
            var ergebnis = Ergebnis(abend, abend.Mehrheit[0], 250);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(umgedeutet, await ServerHashAsync(abend.Gina, umgedeutet.Id)), SyncTestData.New(ergebnis)],
            });

            Assert.IsTrue(result.Accepted.Any(a => a.Id == umgedeutet.Id), "Die erlaubte Umdeutung wurde abgewiesen - sonst misst dieser Test nichts.");
            AssertAccepted(result, ergebnis, "Der Rohwert an der eben umgedeuteten Frage wurde abgewiesen.");
        }

        /// <summary>
        /// Gina teilt ihre Mehrheitsfrage; Bob spielt sie in seinem Spiel und deutet sie im selben Push zur Reaktion um -
        /// verboten, die Frage gehört Gina. Sein Ergebnis darf die Art daraus nicht nehmen.
        /// </summary>
        [TestMethod]
        public async Task AKindFromAForbiddenQuestionDoesNotCount()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory, "Geteilt");
            var mehrheitId = abend.Mehrheit[0].QuestionBaseId!.Value;

            var geteilt = Minispiel(MinigameKind.Majority, abend.Spiel.Category.Id);
            geteilt.Id = mehrheitId;
            geteilt.IsShared = true;
            var teilen = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.Changed(geteilt, await ServerHashAsync(abend.Gina, mehrheitId))] });
            Assert.AreEqual(0, teilen.Rejected.Count, string.Join("; ", teilen.Rejected.Select(r => r.Message)));

            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobFremd");
            var bobsSpiel = SyncTestData.NewGame("Bobs Abend");
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = bobsSpiel.Game.Id, QuestionBaseId = mehrheitId, X = 1, Y = 1 };
            var anlegen = bobsSpiel.AsNewPush();
            anlegen.Upserts.Add(SyncTestData.New(zelle));
            Assert.AreEqual(0, (await PushAsync(bob, anlegen)).Rejected.Count, "Bobs Spiel wurde nicht angelegt.");

            var umgedeutet = Minispiel(MinigameKind.Reaction, abend.Spiel.Category.Id);
            umgedeutet.Id = mehrheitId;
            umgedeutet.IsShared = true;
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = bobsSpiel.Game.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = mehrheitId,
                PlayerId = bobTokens.Account.PlayerId, Score = 100, RightCount = 1, RecordValue = 250, RecordedAt = DateTimeOffset.UtcNow,
            };

            var result = await PushAsync(bob, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(umgedeutet, await ServerHashAsync(bob, mehrheitId)), SyncTestData.New(ergebnis)],
            });

            AssertRejected(result, umgedeutet.Id, SyncRejectReason.Forbidden, "Bob durfte Ginas Frage umdeuten - sonst misst dieser Test nichts.");
            AssertInvalid(result, ergebnis, "Der Rohwert kam über die Art einer verbotenen Frage durch.");
        }
    }
}
