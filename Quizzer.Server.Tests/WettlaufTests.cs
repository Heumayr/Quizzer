using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Wettlauf zwischen Prüfung und Einspielen eines Pushs (M37 P9, P7-2). Der Server prüft jede Zeile gegen ihren Hash und
    /// spielt sie danach ein; dazwischen hält allein die RowVersion der geprüften, verfolgten Zeile.
    /// <para>
    /// Deterministisch über eine Zeilensperre wie in P4. Die Datenbank läuft mit READ_COMMITTED_SNAPSHOT (gemessen an
    /// <c>sys.databases</c>) - Lesen wartet nie, die Bremse muss deshalb ein <b>Schreiben</b> des Pushs sein, das vor dem
    /// Einspielen der geprüften Zeile liegt: das Löschen einer anderen Zeile oder das Freigeben eines Verweises.
    /// </para>
    /// </summary>
    [TestClass]
    public sealed class WettlaufTests
    {
        private const string Fremd = "Fremd geändert";

        private sealed record Abend(HttpClient Gina, Guid Ich, SyncTestData.GameSet Spiel, Header Kopf, Header Kopf2);

        private sealed record Anweisung(string Sql, Guid Id);

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        private static async Task<string> HashAsync(HttpClient client, Guid id)
            => (await SnapshotAsync(client)).Rows.Single(r => r.Id == id).Hash;

        private static async Task<SyncDelete> LoeschenAsync(HttpClient client, string typ, Guid id)
            => new() { Type = typ, Id = id, BaseHash = await HashAsync(client, id) };

        private static async Task<Abend> AbendAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, tokens) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var spiel = SyncTestData.NewGame("Wettlauf");
            var kopf = new Header { Id = Guid.NewGuid(), GameId = spiel.Game.Id, HeaderType = HeaderType.Column, Index = 1, Designation = "Eins" };
            var kopf2 = new Header { Id = Guid.NewGuid(), GameId = spiel.Game.Id, HeaderType = HeaderType.Column, Index = 2, Designation = "Zwei" };

            var push = spiel.AsNewPush();
            push.Upserts.AddRange([SyncTestData.New(kopf), SyncTestData.New(kopf2)]);

            var antwort = await gina.PostAsJsonAsync("/api/v1/sync/push", push);
            Assert.AreEqual(HttpStatusCode.OK, antwort.StatusCode, await antwort.Content.ReadAsStringAsync());
            Assert.AreEqual(0, (await antwort.Content.ReadFromJsonAsync<SyncPushResponse>())!.Rejected.Count);
            return new Abend(gina, tokens.Account.PlayerId, spiel, kopf, kopf2);
        }

        /// <summary>
        /// Schickt den Push, während <paramref name="sperre"/> in einer offenen Transaktion eine Zeile hält. Erst wenn der Push
        /// nachweislich darauf wartet, schreibt <paramref name="zweiter"/> und die Sperre wird zurückgerollt - oder, ohne zweiten
        /// Schreiber, ist die Sperre selbst der Schreiber und wird festgeschrieben.
        /// </summary>
        private static async Task<HttpResponseMessage> PushImWettlaufAsync(HttpClient client, SyncPushRequest push, Anweisung sperre, Anweisung? zweiter)
        {
            await using var bremse = new SqlConnection(ServerTestHost.ConnectionString);
            await bremse.OpenAsync();
            await using var transaktion = (SqlTransaction)await bremse.BeginTransactionAsync();
            await AusfuehrenAsync(bremse, transaktion, sperre);

            await using var spidBefehl = new SqlCommand("SELECT @@SPID", bremse, transaktion);
            var spid = (short)(await spidBefehl.ExecuteScalarAsync())!;

            var laufend = client.PostAsJsonAsync("/api/v1/sync/push", push);

            if (!await WartetAufAsync(spid, laufend))
            {
                await transaktion.RollbackAsync();
                Assert.Fail("Der Push hat nie auf die gesperrte Zeile gewartet - dann misst der Test nichts. " + await BeschreibeAsync(await laufend));
            }

            if (zweiter != null)
            {
                await using var verbindung = new SqlConnection(ServerTestHost.ConnectionString);
                await verbindung.OpenAsync();
                await AusfuehrenAsync(verbindung, null, zweiter);
                await transaktion.RollbackAsync();
            }
            else
            {
                await transaktion.CommitAsync();
            }

            return await laufend;
        }

        private static async Task AusfuehrenAsync(SqlConnection verbindung, SqlTransaction? transaktion, Anweisung anweisung)
        {
            await using var befehl = new SqlCommand(anweisung.Sql, verbindung, transaktion) { CommandTimeout = 10 };
            befehl.Parameters.AddWithValue("@id", anweisung.Id);
            befehl.Parameters.AddWithValue("@fremd", Fremd);
            Assert.AreEqual(1, await befehl.ExecuteNonQueryAsync(), "Die Zeile für " + anweisung.Sql + " fehlt.");
        }

        private static async Task<bool> WartetAufAsync(short spid, Task laufend)
        {
            await using var verbindung = new SqlConnection(ServerTestHost.ConnectionString);
            await verbindung.OpenAsync();

            for (var i = 0; i < 200 && !laufend.IsCompleted; i++)
            {
                await using var befehl = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id = @spid", verbindung);
                befehl.Parameters.AddWithValue("@spid", spid);

                if ((int)(await befehl.ExecuteScalarAsync())! > 0)
                    return true;

                await Task.Delay(50);
            }

            return false;
        }

        private static async Task<string?> DesignationAsync(string tabelle, Guid id)
        {
            await using var verbindung = new SqlConnection(ServerTestHost.ConnectionString);
            await verbindung.OpenAsync();
            await using var befehl = new SqlCommand($"SELECT [Designation] FROM {tabelle} WHERE [Id] = @id", verbindung);
            befehl.Parameters.AddWithValue("@id", id);
            return await befehl.ExecuteScalarAsync() as string;
        }

        private static async Task<string> BeschreibeAsync(HttpResponseMessage antwort)
            => $"{(int)antwort.StatusCode} {await antwort.Content.ReadAsStringAsync()}";

        private static async Task<bool> AngenommenAsync(HttpResponseMessage antwort, Guid id)
            => antwort.StatusCode == HttpStatusCode.OK
               && (await antwort.Content.ReadFromJsonAsync<SyncPushResponse>())!.Accepted.Any(a => a.Id == id);

        private static Anweisung Halten(string tabelle, Guid id) => new($"UPDATE {tabelle} SET [Designation] = [Designation] + N'!' WHERE [Id] = @id", id);

        private static Anweisung Aendern(string tabelle, Guid id) => new($"UPDATE {tabelle} SET [Designation] = @fremd WHERE [Id] = @id", id);

        /// <summary>Eine Frage im Push ändert nichts an ihrem Typ - vor P7-2 leerte schon das die verfolgten Einträge.</summary>
        private static async Task FrageMitschickenAsync(Abend abend, SyncPushRequest push)
        {
            var frage = abend.Spiel.Question;
            var frageHash = await HashAsync(abend.Gina, frage.Id);
            frage.QuestionText = "Neu vom PC?";
            push.Upserts.Add(SyncTestData.Changed(frage, frageHash));
        }

        /// <summary>
        /// Die Kategorie wird geprüft, dann ändert sie ein zweiter Schreiber; gebremst wird der Push am Löschen einer
        /// Kopfzeile, vor dem Einspielen der Kategorie. Mit einer Frage im Push leerte der Server vor dem Einspielen seine
        /// verfolgten Einträge, lud die Kategorie frisch und überschrieb die fremde Änderung still.
        /// </summary>
        [TestMethod]
        [DataRow(true, DisplayName = "mit Frage im Push")]
        [DataRow(false, DisplayName = "ohne Frage im Push")]
        public async Task AConcurrentChangeBetweenCheckAndApplyIsNotOverwritten(bool mitFrage)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var kategorie = abend.Spiel.Category;
            var kategorieHash = await HashAsync(abend.Gina, kategorie.Id);

            kategorie.Designation = "Vom PC";
            var push = new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(kategorie, kategorieHash)],
                Deletes = [await LoeschenAsync(abend.Gina, nameof(Header), abend.Kopf2.Id)],
            };

            if (mitFrage)
                await FrageMitschickenAsync(abend, push);

            var antwort = await PushImWettlaufAsync(abend.Gina, push,
                Halten("[base].[Header]", abend.Kopf2.Id), Aendern("[base].[Category]", kategorie.Id));

            Assert.AreEqual(Fremd, await DesignationAsync("[base].[Category]", kategorie.Id),
                "Die Änderung des zweiten Schreibers ist still überschrieben. " + await BeschreibeAsync(antwort));
            Assert.IsFalse(await AngenommenAsync(antwort, kategorie.Id), "Der Push meldet die Kategorie als übernommen. " + await BeschreibeAsync(antwort));
        }

        /// <summary>
        /// Dasselbe beim Löschen: die Kopfzeile wird geprüft, dann ändert sie ein zweiter Schreiber; gebremst wird der Push, als
        /// er der Zelle die gelöschte Frage abnimmt (P7, P5-2), vor dem Löschen. Mit einer Frage im Push löschte der Server die
        /// Kopfzeile trotzdem, samt der fremden Änderung.
        /// </summary>
        [TestMethod]
        [DataRow(true, DisplayName = "mit Frage im Push")]
        [DataRow(false, DisplayName = "ohne Frage im Push")]
        public async Task AConcurrentChangeBetweenCheckAndDeleteIsNotDeleted(bool mitFrage)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var zelle = abend.Spiel.Cell;
            var zellenHash = await HashAsync(abend.Gina, zelle.Id);

            zelle.QuestionBaseId = null;
            var push = new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(zelle, zellenHash)],
                Deletes =
                [
                    await LoeschenAsync(abend.Gina, nameof(DefaultQuestion), abend.Spiel.Question.Id),
                    await LoeschenAsync(abend.Gina, nameof(Header), abend.Kopf.Id),
                ],
            };

            if (mitFrage)
            {
                push.Upserts.Add(SyncTestData.New(new DefaultQuestion
                {
                    Id = Guid.NewGuid(), Designation = "Andere", QuestionText = "?", CategoryId = abend.Spiel.Category.Id, Points = 100,
                }));
            }

            var antwort = await PushImWettlaufAsync(abend.Gina, push,
                Halten("[base].[GameGridCoordinate]", zelle.Id), Aendern("[base].[Header]", abend.Kopf.Id));

            Assert.AreEqual(Fremd, await DesignationAsync("[base].[Header]", abend.Kopf.Id),
                "Die geänderte Kopfzeile ist gelöscht. " + await BeschreibeAsync(antwort));
        }

        /// <summary>
        /// Eine Frage mit neuem Typ wird geprüft, dann ändert sie ein zweiter Schreiber. Die Umwandlung lief mit eigenem Kontext
        /// außerhalb der Transaktion und ohne Blick auf die RowVersion, danach lud der Server sie frisch - die fremde Änderung
        /// ging still verloren, und scheiterte der Push, blieb die Frage trotzdem umgewandelt.
        /// </summary>
        [TestMethod]
        public async Task AConcurrentChangeBeforeATypeConversionIsNotOverwritten()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var alt = abend.Spiel.Question;
            var frageHash = await HashAsync(abend.Gina, alt.Id);

            var neu = new MultipleChoiceQuestion
            {
                Id = alt.Id, Designation = "Vom PC", QuestionText = alt.QuestionText, CategoryId = alt.CategoryId, Points = alt.Points,
            };

            var push = new SyncPushRequest { Upserts = [SyncTestData.Changed(neu, frageHash)] };

            var antwort = await PushImWettlaufAsync(abend.Gina, push, Aendern("[question].[QuestionBase]", alt.Id), zweiter: null);

            Assert.AreEqual(Fremd, await DesignationAsync("[question].[QuestionBase]", alt.Id),
                "Die Änderung des zweiten Schreibers ist mit der Umwandlung überschrieben. " + await BeschreibeAsync(antwort));
            Assert.AreEqual(nameof(DefaultQuestion), (await SnapshotAsync(abend.Gina)).Rows.Single(r => r.Id == alt.Id).Type,
                "Die Frage ist umgewandelt, obwohl der Push nicht durchging. " + await BeschreibeAsync(antwort));
        }

        /// <summary>
        /// Ohne Wettlauf: Multiple Choice mit zwei Lösungen wird zur Schätzfrage, der PC schickt die Frage und den Schritt, dem
        /// seine Umwandlung die Markierung nahm. Die Umwandlung am Server nimmt sie in derselben Transaktion noch einmal; der
        /// verfolgte Schritt geht mit der vergessenen Frage (gemessen) und wird frisch gelesen - sonst scheiterte der Push an
        /// dessen eigener RowVersion.
        /// </summary>
        [TestMethod]
        public async Task AConversionThatDropsASecondSolutionStillApplies()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var frage = new MultipleChoiceQuestion
            {
                Id = Guid.NewGuid(), Designation = "Zwei Lösungen", QuestionText = "?", CategoryId = abend.Spiel.Category.Id, Points = 100,
            };
            var erste = new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = frage.Id, StepText = "A", SequenceNumber = 1, IsResult = true };
            var zweite = new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = frage.Id, StepText = "B", SequenceNumber = 2, IsResult = true };
            var anlegen = new SyncPushRequest { Upserts = [SyncTestData.New(frage), SyncTestData.New(erste), SyncTestData.New(zweite)] };
            Assert.AreEqual(HttpStatusCode.OK, (await abend.Gina.PostAsJsonAsync("/api/v1/sync/push", anlegen)).StatusCode);

            var schaetzfrage = new AppreciateQestion
            {
                Id = frage.Id, Designation = frage.Designation, QuestionText = frage.QuestionText, CategoryId = frage.CategoryId, Points = frage.Points,
            };
            var zweiteHash = await HashAsync(abend.Gina, zweite.Id);
            zweite.IsResult = false;

            var push = new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(schaetzfrage, await HashAsync(abend.Gina, frage.Id)), SyncTestData.Changed(zweite, zweiteHash)],
            };

            var antwort = await abend.Gina.PostAsJsonAsync("/api/v1/sync/push", push);

            Assert.AreEqual(HttpStatusCode.OK, antwort.StatusCode, await BeschreibeAsync(antwort));
            var ergebnis = (await antwort.Content.ReadFromJsonAsync<SyncPushResponse>())!;
            Assert.AreEqual(0, ergebnis.Rejected.Count, string.Join("; ", ergebnis.Rejected.Select(r => r.Message)));
            var stand = await SnapshotAsync(abend.Gina);
            Assert.AreEqual(nameof(AppreciateQestion), stand.Rows.Single(r => r.Id == frage.Id).Type);
            Assert.AreEqual(1, stand.Rows.Count(r => r.Type == nameof(QuestionStepResource) && r.Values["QuestionBaseId"].GetGuid() == frage.Id
                && r.Values["IsResult"].GetBoolean()), "Die Schätzfrage hat nicht genau eine Lösung.");
        }

        /// <summary>
        /// Ohne Wettlauf: der Push scheitert erst an der Datenbank (ein zweiter Platz derselben Spielerin im selben Spiel).
        /// Die Umwandlung lief bis P7-2 vorher in eigener Transaktion - die Frage blieb umgewandelt, obwohl nichts übernommen war.
        /// </summary>
        [TestMethod]
        public async Task AFailedPushLeavesTheQuestionTypeAlone()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var platz = new SyncPushRequest { Upserts = [SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = abend.Spiel.Game.Id, PlayerId = abend.Ich })] };
            Assert.AreEqual(HttpStatusCode.OK, (await abend.Gina.PostAsJsonAsync("/api/v1/sync/push", platz)).StatusCode);

            var alt = abend.Spiel.Question;
            var neu = new MultipleChoiceQuestion
            {
                Id = alt.Id, Designation = "Vom PC", QuestionText = alt.QuestionText, CategoryId = alt.CategoryId, Points = alt.Points,
            };

            var push = new SyncPushRequest
            {
                Upserts =
                [
                    SyncTestData.Changed(neu, await HashAsync(abend.Gina, alt.Id)),
                    SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = abend.Spiel.Game.Id, PlayerId = abend.Ich }),
                ],
            };

            var antwort = await abend.Gina.PostAsJsonAsync("/api/v1/sync/push", push);

            Assert.AreEqual(HttpStatusCode.Conflict, antwort.StatusCode, "Der doppelte Platz scheitert nicht an der Datenbank - dann misst der Test nichts. " + await BeschreibeAsync(antwort));
            Assert.AreEqual(nameof(DefaultQuestion), (await SnapshotAsync(abend.Gina)).Rows.Single(r => r.Id == alt.Id).Type,
                "Die Frage ist umgewandelt, obwohl der Push nicht durchging.");
        }
    }
}
