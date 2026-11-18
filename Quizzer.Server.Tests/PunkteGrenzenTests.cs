using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Punkte-Parameter im Abgleich (M37 P5, R3-3): nie negativ - wie die Maske. Ein negativer Faktor am Spiel oder negative
    /// Punkte an der Frage machten den Feldwert negativ; eine Teamregel teilte dann Minus aus. Kostet nur die Zeile (<c>Invalid</c>).
    /// </summary>
    [TestClass]
    public sealed class PunkteGrenzenTests
    {
        private static readonly string[] SpielFelder =
        [
            nameof(Game.DifficultyMultiplier), nameof(Game.DifficultyAddition), nameof(Game.DifficultyMinusMultiplier),
            nameof(Game.DifficultyMinusAddition), nameof(Game.PhaseMultiplier), nameof(Game.PhaseAddition),
        ];

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static SyncUpsert Mit(SyncUpsert upsert, string feld, double wert)
        {
            Assert.IsTrue(upsert.Row.Values.ContainsKey(feld), $"{upsert.Row.Type}.{feld} steht nicht in der Zeile - die Probe misst nichts.");
            upsert.Row.Values[feld] = JsonSerializer.SerializeToElement(wert);
            return upsert;
        }

        [TestMethod]
        public async Task ANegativeGameParameterIsInvalidZeroIsNot()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");

            foreach (var feld in SpielFelder)
            {
                var negativ = SyncTestData.NewGame("Negativ " + feld);
                var null_ = SyncTestData.NewGame("Null " + feld);

                var result = await PushAsync(gina, new SyncPushRequest
                {
                    Upserts = [Mit(SyncTestData.New(negativ.Game), feld, -1), Mit(SyncTestData.New(null_.Game), feld, 0)],
                });

                Assert.IsTrue(result.Rejected.Any(r => r.Id == negativ.Game.Id && r.Reason == SyncRejectReason.Invalid), $"{feld} = -1 ging durch.");
                Assert.IsTrue(result.Accepted.Any(a => a.Id == null_.Game.Id), $"{feld} = 0 wurde abgewiesen: " + string.Join("; ", result.Rejected.Select(r => r.Message)));
            }
        }

        /// <summary>Auch eine bestehende Zeile: ein Spiel nachträglich ins Negative gedreht.</summary>
        [TestMethod]
        public async Task AnExistingGameCannotTurnNegative()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var set = SyncTestData.NewGame("Bestand");
            Assert.AreEqual(0, (await PushAsync(gina, set.AsNewPush())).Rejected.Count);
            var hash = (await gina.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!.Rows.Single(r => r.Id == set.Game.Id).Hash;
            set.Game.PhaseAddition = -100;

            var result = await PushAsync(gina, new SyncPushRequest { Upserts = [SyncTestData.Changed(set.Game, hash)] });

            Assert.IsTrue(result.Rejected.Any(r => r.Id == set.Game.Id && r.Reason == SyncRejectReason.Invalid), "Das Spiel wurde ins Negative gedreht.");
        }

        [TestMethod]
        [DataRow(nameof(QuestionBase.Points))]
        [DataRow(nameof(QuestionBase.MinusPoints))]
        public async Task ANegativeQuestionValueIsInvalid(string feld)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var set = SyncTestData.NewGame("Frage " + feld);
            var push = set.AsNewPush();
            Mit(push.Upserts.Single(u => u.Row.Id == set.Question.Id), feld, -1);
            push.Upserts.RemoveAll(u => u.Row.Id == set.Cell.Id);

            var result = await PushAsync(gina, push);

            Assert.IsTrue(result.Rejected.Any(r => r.Id == set.Question.Id && r.Reason == SyncRejectReason.Invalid), $"{nameof(DefaultQuestion)}.{feld} = -1 ging durch.");
        }

        /// <summary>
        /// P5-5: die gespeicherten Punkte einer Zelle. Eine gespielte Zelle rechnet beim Öffnen nie neu - gemessen stand beim
        /// Nachbessern „richtig −500, falsch +300" auf der Karte. Eine offene rechnet neu; abgewiesen wird trotzdem jede.
        /// </summary>
        [TestMethod]
        [DataRow(nameof(GameGridCoordinate.CurrentPoints))]
        [DataRow(nameof(GameGridCoordinate.CurrentMinusPoints))]
        public async Task ANegativeStoredCellValueIsInvalidZeroIsNot(string feld)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var negativ = SyncTestData.NewGame("Zelle negativ " + feld);
            var null_ = SyncTestData.NewGame("Zelle null " + feld);
            negativ.Cell.IsDone = true;
            null_.Cell.IsDone = true;
            var push = negativ.AsNewPush();
            push.Upserts.AddRange(null_.AsNewPush().Upserts);
            Mit(push.Upserts.Single(u => u.Row.Id == negativ.Cell.Id), feld, -1);
            Mit(push.Upserts.Single(u => u.Row.Id == null_.Cell.Id), feld, 0);

            var result = await PushAsync(gina, push);

            Assert.IsTrue(result.Rejected.Any(r => r.Id == negativ.Cell.Id && r.Reason == SyncRejectReason.Invalid), $"{nameof(GameGridCoordinate)}.{feld} = -1 ging durch.");
            Assert.IsTrue(result.Accepted.Any(a => a.Id == null_.Cell.Id), $"{feld} = 0 wurde abgewiesen: " + string.Join("; ", result.Rejected.Select(r => r.Message)));
        }
    }
}
