using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Was die Sicherheitsprüfung vom 2026-10-21 gefunden hat - jeder Angriff nachgestellt gegen
    /// den Server im Prozess.
    /// </summary>
    [TestClass]
    public sealed class SecurityFindingsTests
    {
        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        /// <summary>
        /// Ein Dateifeld mit Pfad statt Namen: auf dem WPF des Opfers würde daraus ein UNC-Pfad,
        /// und Windows schickte beim Anzeigen den Anmeldehash an den fremden Rechner.
        /// </summary>
        [TestMethod]
        [DataRow(@"\\angreifer.example\s\bild.png")]
        [DataRow(@"C:\Windows\win.ini")]
        [DataRow("../../geheim.png")]
        [DataRow("Resources/../x.png")]
        public async Task AFileFieldWithAPathIsRefused(string boese)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (mallory, _) = await ServerTestHost.NewMemberAsync(factory, "Mallory");

            var gast = new Player { Id = Guid.NewGuid(), Designation = "Gast", UserPictureFileName = boese };
            var set = SyncTestData.NewGame("Bilder");
            var schritt = new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = set.Question.Id, StepText = "x", ResourceFileName = boese, ResourceTyp = DataModels.Enumerations.ResourceType.Image };

            var push = set.AsNewPush();
            push.Upserts.Add(SyncTestData.New(gast));
            push.Upserts.Add(SyncTestData.New(schritt));

            var result = await PushAsync(mallory, push);

            Assert.IsTrue(result.Rejected.Any(r => r.Id == gast.Id && r.Reason == SyncRejectReason.Invalid), "Das Spielerbild wurde angenommen.");
            Assert.IsTrue(result.Rejected.Any(r => r.Id == schritt.Id && r.Reason == SyncRejectReason.Invalid), "Das Schritt-Medium wurde angenommen.");
        }

        [TestMethod]
        public async Task APlainFileNameIsStillAccepted()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var gast = new Player { Id = Guid.NewGuid(), Designation = "Gast", UserPictureFileName = "gast foto (1).png" };

            var result = await PushAsync(alice, new SyncPushRequest { Upserts = [SyncTestData.New(gast)] });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
        }

        /// <summary>Kommt so etwas doch vom Server, leert der Client das Feld vor dem Einspielen.</summary>
        [TestMethod]
        public void TheClientDefusesAPathComingFromTheServer()
        {
            var row = SyncTestData.Row(new Player { Id = Guid.NewGuid(), Designation = "Gast", UserPictureFileName = @"\\angreifer\s\p.png" });

            Assert.IsTrue(SyncDateifelder.Entschaerfe(row));
            Assert.AreEqual(string.Empty, row.Values[nameof(Player.UserPictureFileName)].GetString());
            Assert.IsNull(SyncDateifelder.Pruefe(row));
        }

        /// <summary>
        /// Eine abgelehnte Fragezeile mit der Id einer fremden privaten Frage öffnete den Verweis
        /// darauf - und mit ihm die Frage im nächsten Abholen.
        /// </summary>
        [TestMethod]
        public async Task ARejectedQuestionRowDoesNotOpenAReferenceToAForeignQuestion()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (mallory, _) = await ServerTestHost.NewMemberAsync(factory, "Mallory");

            var geheim = SyncTestData.NewGame("Geheim");
            await PushAsync(alice, geheim.AsNewPush());

            var mine = SyncTestData.NewGame("Mallorys");
            var push = mine.AsNewPush();

            // Dieselbe Id wie Alices Frage, als "neu" geschickt - wird abgelehnt, stand aber in
            // der Liste der neuen Fragen.
            push.Upserts.Add(SyncTestData.New(new DataModels.Models.QuestionTypes.DefaultQuestion
            {
                Id = geheim.Question.Id, CategoryId = mine.Category.Id, Designation = "Tarnung",
            }));

            var dieb = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = mine.Game.Id, QuestionBaseId = geheim.Question.Id, X = 1, Y = 0 };
            push.Upserts.Add(SyncTestData.New(dieb));

            var result = await PushAsync(mallory, push);

            Assert.IsTrue(result.Rejected.Any(r => r.Id == dieb.Id), "Die Zelle auf Alices Frage wurde angenommen.");
            Assert.IsFalse((await SnapshotAsync(mallory)).Rows.Any(r => r.Id == geheim.Question.Id), "Mallory sieht Alices Frage.");
        }

        /// <summary>Ein Ergebnis darf nicht an der Zelle eines anderen Spiels hängen.</summary>
        [TestMethod]
        public async Task AResultCannotHangOnACellOfAnotherGame()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");
            var (mallory, malloryTokens) = await ServerTestHost.NewMemberAsync(factory, "Mallory");

            var bobs = SyncTestData.NewGame("Bobs");
            await PushAsync(bob, bobs.AsNewPush());

            var mine = SyncTestData.NewGame("Mallorys");
            var push = mine.AsNewPush();

            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(),
                GameId = mine.Game.Id,
                QuestionBaseId = mine.Question.Id,
                PlayerId = malloryTokens.Account.PlayerId,
                GameGridCoordinateId = bobs.Cell.Id,
            };
            push.Upserts.Add(SyncTestData.New(ergebnis));

            var result = await PushAsync(mallory, push);

            Assert.IsTrue(result.Rejected.Any(r => r.Id == ergebnis.Id && r.Reason == SyncRejectReason.Forbidden),
                "Das Ergebnis an Bobs Zelle wurde angenommen - Bob koennte sein Spiel nie mehr loeschen.");
        }

        /// <summary>Ein bestehendes Design ändert nur der Admin.</summary>
        [TestMethod]
        public async Task AMemberCannotChangeAnExistingTheme()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (mallory, _) = await ServerTestHost.NewMemberAsync(factory, "Mallory");

            var design = new GameTheme { Id = Guid.NewGuid(), Designation = "Abend", FolderName = "Abend" };
            var angelegt = await PushAsync(alice, new SyncPushRequest { Upserts = [SyncTestData.New(design)] });
            Assert.AreEqual(0, angelegt.Rejected.Count, "Ein neues Design darf jeder anlegen.");

            var hash = (await SnapshotAsync(mallory)).Rows.Single(r => r.Id == design.Id).Hash;
            design.FolderName = "Standard";

            var result = await PushAsync(mallory, new SyncPushRequest { Upserts = [SyncTestData.Changed(design, hash)] });

            Assert.IsTrue(result.Rejected.Any(r => r.Id == design.Id && r.Reason == SyncRejectReason.Forbidden));
        }
    }
}
