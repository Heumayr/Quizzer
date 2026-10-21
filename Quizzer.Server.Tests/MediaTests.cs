using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    [TestClass]
    public sealed class MediaTests
    {
        private static ByteArrayContent Bytes(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

        [TestMethod]
        public async Task PathTricksAreRefused()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            foreach (var path in new[] { "../boese.png", "Resources/../../boese.png", "/etc/passwd", "Resources/boese.exe", "Andere/x.png", "Resources\\..\\x.png" })
            {
                var response = await alice.PutAsync("/api/v1/media/content?path=" + Uri.EscapeDataString(path), Bytes("x"));
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, path);
            }
        }

        [TestMethod]
        public async Task AFileIsNeverOverwritten()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var path = $"Resources/bild_{Guid.NewGuid():N}.png";

            Assert.AreEqual(HttpStatusCode.Created, (await alice.PutAsync("/api/v1/media/content?path=" + path, Bytes("eins"))).StatusCode);
            Assert.AreEqual(HttpStatusCode.NoContent, (await alice.PutAsync("/api/v1/media/content?path=" + path, Bytes("eins"))).StatusCode);
            Assert.AreEqual(HttpStatusCode.Conflict, (await alice.PutAsync("/api/v1/media/content?path=" + path, Bytes("zwei"))).StatusCode);
        }

        [TestMethod]
        public async Task OnlyMediaOfVisibleQuestionsCanBeFetched()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");

            var fileName = $"geheim_{Guid.NewGuid():N}.png";
            var set = SyncTestData.NewGame("Medien");
            var step = new QuestionStepResource
            {
                Id = Guid.NewGuid(), QuestionBaseId = set.Question.Id, ResourceFileName = fileName, Designation = "Bild",
            };
            var push = set.AsNewPush();
            push.Upserts.Add(SyncTestData.New(step));

            var pushed = await alice.PostAsJsonAsync("/api/v1/sync/push", push);
            var pushResult = (await pushed.Content.ReadFromJsonAsync<SyncPushResponse>())!;
            Assert.AreEqual(0, pushResult.Rejected.Count, string.Join("; ", pushResult.Rejected.Select(r => r.Message)));

            var path = "Resources/" + fileName;
            Assert.AreEqual(HttpStatusCode.Created, (await alice.PutAsync("/api/v1/media/content?path=" + path, Bytes("bild"))).StatusCode);

            Assert.AreEqual(HttpStatusCode.OK, (await alice.GetAsync("/api/v1/media/content?path=" + path)).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await bob.GetAsync("/api/v1/media/content?path=" + path)).StatusCode);

            var manifest = (await bob.GetFromJsonAsync<List<MediaFileInfo>>("/api/v1/media"))!;
            Assert.IsFalse(manifest.Any(m => m.Path == path));
        }
    }
}
