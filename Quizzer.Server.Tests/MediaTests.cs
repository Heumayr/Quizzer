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

        /// <summary>Legt eine eigene Frage an, deren Schritt die Datei verwendet.</summary>
        private static async Task PushStepWithMediumAsync(HttpClient client, string fileName)
        {
            var set = SyncTestData.NewGame("Medium");
            var push = set.AsNewPush();
            push.Upserts.Add(SyncTestData.New(new QuestionStepResource
            {
                Id = Guid.NewGuid(), QuestionBaseId = set.Question.Id, ResourceFileName = fileName, Designation = "Bild",
            }));

            var response = await client.PostAsJsonAsync("/api/v1/sync/push", push);
            var result = (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
        }

        /// <summary>
        /// Sicherheitspruefung 2026-10-21: abgelegt wird nur, was eine Zeile verwendet, die das
        /// Konto schreiben darf - keine fremden, keine unbenutzten Dateien, keine Grundtexturen,
        /// in Design-Ordnern nur die bekannten Texturnamen.
        /// </summary>
        [TestMethod]
        public async Task OnlyFilesOfOwnRowsCanBeUploaded()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (mallory, _) = await ServerTestHost.NewMemberAsync(factory, "Mallory");

            var alicesDatei = $"alice_{Guid.NewGuid():N}.png";
            await PushStepWithMediumAsync(alice, alicesDatei);

            async Task<HttpStatusCode> Hoch(HttpClient c, string pfad)
                => (await c.PutAsync("/api/v1/media/content?path=" + pfad, Bytes("x"))).StatusCode;

            Assert.AreEqual(HttpStatusCode.Forbidden, await Hoch(mallory, $"Resources/frei_{Guid.NewGuid():N}.png"), "Unbenutzte Datei angenommen.");
            Assert.AreEqual(HttpStatusCode.Forbidden, await Hoch(mallory, "Resources/" + alicesDatei), "Datei einer fremden Frage angenommen.");
            Assert.AreEqual(HttpStatusCode.Forbidden, await Hoch(mallory, "Background.png"), "Grundtextur von einem Mitglied angenommen.");

            var design = new GameTheme { Id = Guid.NewGuid(), Designation = "Abend", FolderName = "Abend" + Guid.NewGuid().ToString("N")[..6] };
            await mallory.PostAsJsonAsync("/api/v1/sync/push", new SyncPushRequest { Upserts = [SyncTestData.New(design)] });

            Assert.AreEqual(HttpStatusCode.Forbidden, await Hoch(mallory, $"Themes/{design.FolderName}/beliebig.png"), "Fremder Name im Design-Ordner angenommen.");
            Assert.AreEqual(HttpStatusCode.Created, await Hoch(mallory, $"Themes/{design.FolderName}/Background.png"), "Bekannte Textur im eigenen Design abgewiesen.");

            Assert.AreEqual(HttpStatusCode.Created, await Hoch(alice, "Resources/" + alicesDatei), "Die eigene Datei wurde abgewiesen.");
        }

        /// <summary>Eine Schreibvariante eines vorhandenen Namens wird nicht neben ihn gelegt.</summary>
        [TestMethod]
        public async Task ACaseVariantOfAnExistingFileIsRefused()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var name = $"Bild_{Guid.NewGuid():N}.png";
            await PushStepWithMediumAsync(alice, name);
            await PushStepWithMediumAsync(alice, name.ToUpperInvariant().Replace(".PNG", ".png"));

            Assert.AreEqual(HttpStatusCode.Created, (await alice.PutAsync("/api/v1/media/content?path=Resources/" + name, Bytes("eins"))).StatusCode);

            var variante = name.ToUpperInvariant().Replace(".PNG", ".png");
            Assert.AreEqual(HttpStatusCode.Conflict, (await alice.PutAsync("/api/v1/media/content?path=Resources/" + variante, Bytes("zwei"))).StatusCode,
                "Die Schreibvariante wurde neben die vorhandene Datei gelegt.");
        }

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
            var name = $"bild_{Guid.NewGuid():N}.png";
            var path = "Resources/" + name;

            // Seit 2026-10-21 nimmt der Server nur Dateien an, die eine eigene Zeile verwendet.
            await PushStepWithMediumAsync(alice, name);

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
