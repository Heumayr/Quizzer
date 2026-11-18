using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Raster und Phasen im Abgleich (M37 P7, P5-4): nie negativ - wie die Maske und das Bündel. Eine negative Zellengröße warf im
    /// Spielfeld; 0 bleibt erlaubt. Kostet nur die Zeile (<c>Invalid</c>). Muster: <see cref="PunkteGrenzenTests"/>.
    /// </summary>
    [TestClass]
    public sealed class RasterGrenzenTests
    {
        private static readonly string[] SpielFelder =
        [
            nameof(Game.Height), nameof(Game.Width), nameof(Game.CellHeight), nameof(Game.CellWidth), nameof(Game.SuggestedPhases),
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
        public async Task ANegativeGridValueIsInvalidZeroIsNot()
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
    }
}
