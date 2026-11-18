using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Ein Elternteil löschen, an dem am Server noch ein Kind hängt (M37 P7, P5-2). Der Server verfolgt jede Zeile des Pushs, auch
    /// eine abgewiesene; EF setzte beim Löschen den Verweis eines verfolgten Kindes still auf null, statt das Löschen am NO ACTION
    /// scheitern zu lassen. Frei wird nur, was derselbe Push selbst umhängt - wie das Ergebnis eines Teams (R1-1).
    /// Muster: <see cref="TeamTests"/>.
    /// </summary>
    [TestClass]
    public sealed class LoeschverweisTests
    {
        /// <summary>Gina leitet; Max und Gustav sitzen als Gäste; die Zelle zeigt auf die Frage.</summary>
        private sealed record Abend(HttpClient Gina, HttpClient Admin, SyncTestData.GameSet Spiel, Player Max, Player Gustav);

        private static async Task<HttpResponseMessage> PushRawAsync(HttpClient client, SyncPushRequest request)
            => await client.PostAsJsonAsync("/api/v1/sync/push", request);

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await PushRawAsync(client, request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        private static async Task<string> HashAsync(HttpClient client, Guid id)
            => (await SnapshotAsync(client)).Rows.Single(r => r.Id == id).Hash;

        private static SyncDelete Loeschen(string typ, Guid id, string hash) => new() { Type = typ, Id = id, BaseHash = hash };

        private static Guid? VerweisAmServer(SyncSnapshot stand, Guid zeile, string feld)
        {
            var wert = stand.Rows.Single(r => r.Id == zeile).Values[feld];
            return wert.ValueKind == JsonValueKind.Null ? null : wert.GetGuid();
        }

        private static async Task<Abend> AbendAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var admin = ServerTestHost.Authorized(factory,
                await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var spiel = SyncTestData.NewGame("Verweisabend");
            var max = new Player { Id = Guid.NewGuid(), Designation = "Max", DisplayName = "Max" };
            var gustav = new Player { Id = Guid.NewGuid(), Designation = "Gustav", DisplayName = "Gustav" };

            var push = spiel.AsNewPush();
            push.Upserts.AddRange([SyncTestData.New(max), SyncTestData.New(gustav),
                SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = max.Id }),
                SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = gustav.Id })]);

            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));
            return new Abend(gina, admin, spiel, max, gustav);
        }

        /// <summary>Ändert die Zeile am Server und gibt den Hash davor zurück - eine Zeile darauf ist ein Konflikt.</summary>
        private static async Task<string> VeraltenAsync(HttpClient client, Guid id, object zeile, Action aendern)
        {
            var vorher = await HashAsync(client, id);
            aendern();
            Assert.AreEqual(0, (await PushAsync(client, new SyncPushRequest { Upserts = [SyncTestData.Changed(zeile, vorher)] })).Rejected.Count);
            return vorher;
        }

        private static async Task<string> MeldungAsync(HttpResponseMessage antwort)
            => $"{(int)antwort.StatusCode} {await antwort.Content.ReadAsStringAsync()}";

        /// <summary>Die Zelle ohne Frage und die Frage gelöscht - wahlweise mit einer zweiten Frage im Push, dann leert der Server seine verfolgten Einträge (Fragetyp umwandeln).</summary>
        private static async Task<SyncPushRequest> FrageLoeschenAsync(Abend abend, string zellenHash, bool mitFrage)
        {
            abend.Spiel.Cell.QuestionBaseId = null;
            var frage = abend.Spiel.Question;

            var push = new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(abend.Spiel.Cell, zellenHash)],
                Deletes = [Loeschen(frage.GetType().Name, frage.Id, await HashAsync(abend.Gina, frage.Id))],
            };

            if (mitFrage)
            {
                push.Upserts.Add(SyncTestData.New(new Quizzer.DataModels.Models.QuestionTypes.DefaultQuestion
                {
                    Id = Guid.NewGuid(), Designation = "Andere", QuestionText = "?", CategoryId = abend.Spiel.Category.Id, Points = 100,
                }));
            }

            return push;
        }

        /// <summary>
        /// Die Zelle wurde anderswo geändert, der PC nimmt ihr auf altem Stand die Frage und löscht sie. Die Zelle kommt als
        /// Konflikt zurück - also zeigt sie am Server weiter auf die Frage, und die Frage bleibt. Vorher verlor die Zelle still
        /// ihre Frage, solange keine Frage im Push stand.
        /// </summary>
        [TestMethod]
        [DataRow(false, DisplayName = "ohne Frage im Push")]
        [DataRow(true, DisplayName = "mit Frage im Push")]
        public async Task ARejectedCellKeepsTheQuestionThePushWantsToDelete(bool mitFrage)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var zelle = abend.Spiel.Cell;
            var veraltet = await VeraltenAsync(abend.Gina, zelle.Id, zelle, () => zelle.SpecialRuleAnnounced = true);

            var antwort = await PushRawAsync(abend.Gina, await FrageLoeschenAsync(abend, veraltet, mitFrage));

            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == abend.Spiel.Question.Id), "Die Frage ist gelöscht. " + await MeldungAsync(antwort));
            Assert.AreEqual(abend.Spiel.Question.Id, VerweisAmServer(stand, zelle.Id, nameof(GameGridCoordinate.QuestionBaseId)),
                "Die abgewiesene Zelle hat still ihre Frage verloren. " + await MeldungAsync(antwort));
        }

        /// <summary>
        /// Die Gegenrichtung: derselbe Push nimmt der Zelle die Frage auf aktuellem Stand und löscht sie - das geht durch, auch mit
        /// einer Frage im Push (dann trägt nur das ausdrückliche Freigeben, nicht EF).
        /// </summary>
        [TestMethod]
        [DataRow(false, DisplayName = "ohne Frage im Push")]
        [DataRow(true, DisplayName = "mit Frage im Push")]
        public async Task ACellReleasedInTheSamePushLetsItsQuestionGo(bool mitFrage)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var result = await PushAsync(abend.Gina, await FrageLoeschenAsync(abend, await HashAsync(abend.Gina, abend.Spiel.Cell.Id), mitFrage));

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsFalse(stand.Rows.Any(r => r.Id == abend.Spiel.Question.Id), "Die Frage steht noch da.");
            Assert.IsNull(VerweisAmServer(stand, abend.Spiel.Cell.Id, nameof(GameGridCoordinate.QuestionBaseId)), "Die Zelle zeigt noch auf die Frage.");
        }

        /// <summary>Der Joker ist an der Zelle eingelöst und wurde anderswo geändert; der PC löscht die Zelle und schickt den Joker auf altem Stand.</summary>
        [TestMethod]
        public async Task ARejectedJokerKeepsTheCellThePushWantsToDelete()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = abend.Spiel.Game.Id, PlayerId = abend.Max.Id, Kind = JokerKind.Double, Reason = "Minispiel",
                GrantedAt = DateTimeOffset.UtcNow, GameGridCoordinateId = abend.Spiel.Cell.Id, RedeemedAt = DateTimeOffset.UtcNow,
            };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(joker)] })).Rejected.Count);
            var veraltet = await VeraltenAsync(abend.Gina, joker.Id, joker, () => joker.Reason = "Minispiel gewonnen");
            joker.Reason = "nachgetragen";

            var antwort = await PushRawAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(joker, veraltet)],
                Deletes = [Loeschen(nameof(GameGridCoordinate), abend.Spiel.Cell.Id, await HashAsync(abend.Gina, abend.Spiel.Cell.Id))],
            });

            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == abend.Spiel.Cell.Id), "Die Zelle ist gelöscht. " + await MeldungAsync(antwort));
            Assert.AreEqual(abend.Spiel.Cell.Id, VerweisAmServer(stand, joker.Id, nameof(PlayerJoker.GameGridCoordinateId)),
                "Der abgewiesene Joker hat still seine Zelle verloren. " + await MeldungAsync(antwort));
        }

        /// <summary>Der Admin löscht den Gast, gegen den eine Sperre läuft, und schickt die Sperre auf altem Stand.</summary>
        [TestMethod]
        public async Task ARejectedBlockKeepsTheTargetThePushWantsToDelete()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var sperre = new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = abend.Spiel.Game.Id, PlayerId = abend.Gustav.Id, Kind = JokerKind.Block, Reason = "Minispiel",
                GrantedAt = DateTimeOffset.UtcNow, TargetPlayerId = abend.Max.Id,
            };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(sperre)] })).Rejected.Count);
            var veraltet = await VeraltenAsync(abend.Gina, sperre.Id, sperre, () => sperre.Reason = "Minispiel gewonnen");
            sperre.Reason = "nachgetragen";

            var antwort = await PushRawAsync(abend.Admin, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(sperre, veraltet)],
                Deletes = [Loeschen(nameof(Player), abend.Max.Id, await HashAsync(abend.Admin, abend.Max.Id))],
            });

            var stand = await SnapshotAsync(abend.Admin);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == abend.Max.Id), "Max ist gelöscht. " + await MeldungAsync(antwort));
            Assert.AreEqual(abend.Max.Id, VerweisAmServer(stand, sperre.Id, nameof(PlayerJoker.TargetPlayerId)),
                "Die abgewiesene Sperre hat still ihr Ziel verloren. " + await MeldungAsync(antwort));
        }

        /// <summary>Der Admin löscht ein Design, das ein Spiel trägt, und schickt das Spiel auf altem Stand.</summary>
        [TestMethod]
        public async Task ARejectedGameKeepsTheThemeThePushWantsToDelete()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var design = new GameTheme { Id = Guid.NewGuid(), Designation = "Abend", FolderName = "Abend" + Guid.NewGuid().ToString("N")[..6] };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(design)] })).Rejected.Count);
            var spiel = abend.Spiel.Game;
            spiel.ModeratorPlayerId = VerweisAmServer(await SnapshotAsync(abend.Gina), spiel.Id, nameof(Game.ModeratorPlayerId));
            await VeraltenAsync(abend.Gina, spiel.Id, spiel, () => spiel.GameThemeId = design.Id);
            var veraltet = await VeraltenAsync(abend.Gina, spiel.Id, spiel, () => spiel.Designation = "Tafel aufgebaut");
            spiel.Designation = "nachgetragen";

            var antwort = await PushRawAsync(abend.Admin, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(spiel, veraltet)],
                Deletes = [Loeschen(nameof(GameTheme), design.Id, await HashAsync(abend.Admin, design.Id))],
            });

            var stand = await SnapshotAsync(abend.Admin);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == design.Id), "Das Design ist gelöscht. " + await MeldungAsync(antwort));
            Assert.AreEqual(design.Id, VerweisAmServer(stand, spiel.Id, nameof(Game.GameThemeId)),
                "Das abgewiesene Spiel hat still sein Design verloren. " + await MeldungAsync(antwort));
        }

        /// <summary>
        /// Pflichtverweis: ein Ergebnis an der Zelle, auf altem Stand geschickt, während die Zelle gelöscht wird. Vorher scheiterte
        /// der Push mit 500 statt 409 - und der Client fällt nur bei 409 auf Einzelzeilen zurück.
        /// </summary>
        [TestMethod]
        public async Task ARejectedResultKeepsTheCellThePushWantsToDelete()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = abend.Spiel.Game.Id, GameGridCoordinateId = abend.Spiel.Cell.Id,
                QuestionBaseId = abend.Spiel.Question.Id, PlayerId = abend.Max.Id, Score = 100,
            };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);
            var veraltet = await VeraltenAsync(abend.Gina, ergebnis.Id, ergebnis, () => ergebnis.Score = 200);
            ergebnis.Score = 300;

            var antwort = await PushRawAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(ergebnis, veraltet)],
                Deletes = [Loeschen(nameof(GameGridCoordinate), abend.Spiel.Cell.Id, await HashAsync(abend.Gina, abend.Spiel.Cell.Id))],
            });

            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == abend.Spiel.Cell.Id), "Die Zelle ist gelöscht. " + await MeldungAsync(antwort));
            Assert.AreEqual(HttpStatusCode.Conflict, antwort.StatusCode, "Der Push scheitert nicht als Konflikt. " + await MeldungAsync(antwort));
        }
    }
}
