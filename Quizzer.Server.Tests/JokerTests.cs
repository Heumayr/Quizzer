using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Joker am Server (M35 C7a): schreiben darf nur der Gamemaster, der Joker gehört einem Mitspieler,
    /// eine Sperre trifft nur einen Mitspieler, eine Zelle stammt aus demselben Spiel. Lesen wie die
    /// übrigen Zeilen eines Spiels. Muster: <see cref="SecurityFindingsTests"/>.
    /// </summary>
    [TestClass]
    public sealed class JokerTests
    {
        /// <summary>Gina leitet; Mia (Konto) und Max (Gast) spielen mit; Gustav ist Gast ohne Platz; Mallory ist fremd.</summary>
        private sealed record Abend(
            HttpClient Gina, HttpClient Mia, Guid MiaId, HttpClient Mallory, Guid MalloryId,
            Guid MaxId, Guid GustavId, SyncTestData.GameSet Spiel);

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        private static async Task<string> HashAsync(HttpClient client, Guid id)
            => (await SnapshotAsync(client)).Rows.Single(r => r.Id == id).Hash;

        private static async Task<Abend> AbendAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "Mia");
            var (mallory, malloryTokens) = await ServerTestHost.NewMemberAsync(factory, "Mallory");

            var spiel = SyncTestData.NewGame("Jokerabend");
            var max = new Player { Id = Guid.NewGuid(), Designation = "Max", DisplayName = "Max" };
            var gustav = new Player { Id = Guid.NewGuid(), Designation = "Gustav", DisplayName = "Gustav" };

            var push = spiel.AsNewPush();
            push.Upserts.Add(SyncTestData.New(max));
            push.Upserts.Add(SyncTestData.New(gustav));
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = miaTokens.Account.PlayerId }));
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = max.Id }));

            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            return new Abend(gina, mia, miaTokens.Account.PlayerId, mallory, malloryTokens.Account.PlayerId, max.Id, gustav.Id, spiel);
        }

        private static PlayerJoker Joker(Abend abend, Guid spieler, JokerKind art = JokerKind.Double) => new()
        {
            Id = Guid.NewGuid(),
            GameId = abend.Spiel.Game.Id,
            PlayerId = spieler,
            Kind = art,
            Reason = "Minispiel gewonnen",
            GrantedAt = DateTimeOffset.UtcNow,
        };

        private static async Task<PlayerJoker> GegebenAsync(Abend abend, PlayerJoker joker)
        {
            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(joker)] });
            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            return joker;
        }

        private static void AssertForbidden(SyncPushResponse result, Guid id, string warum)
            => Assert.IsTrue(result.Rejected.Any(r => r.Id == id && r.Reason == SyncRejectReason.Forbidden), warum);

        [TestMethod]
        public async Task AForeignAccountDoesNotSeeTheJokersButAMemberDoes()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = await GegebenAsync(abend, Joker(abend, abend.MiaId));

            Assert.IsFalse((await SnapshotAsync(abend.Mallory)).Rows.Any(r => r.Id == joker.Id), "Mallory sieht einen Joker eines fremden Spiels.");
            Assert.IsTrue((await SnapshotAsync(abend.Mia)).Rows.Any(r => r.Id == joker.Id), "Die Mitspielerin sieht die Joker ihres Spiels nicht.");
        }

        [TestMethod]
        public async Task AForeignAccountCannotCreateAJokerInAForeignGame()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = Joker(abend, abend.MiaId);

            var result = await PushAsync(abend.Mallory, new SyncPushRequest { Upserts = [SyncTestData.New(joker)] });

            AssertForbidden(result, joker.Id, "Mallory hat in Ginas Spiel einen Joker vergeben.");
            Assert.IsFalse((await SnapshotAsync(abend.Gina)).Rows.Any(r => r.Id == joker.Id));
        }

        /// <summary>Mitspielen heißt nicht leiten: sich selbst einen Joker geben darf nur der Gamemaster.</summary>
        [TestMethod]
        public async Task AMemberCannotGrantHimselfAJoker()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = Joker(abend, abend.MiaId);

            var result = await PushAsync(abend.Mia, new SyncPushRequest { Upserts = [SyncTestData.New(joker)] });

            AssertForbidden(result, joker.Id, "Mia hat sich selbst einen Joker gegeben.");
        }

        /// <summary>Mit dem richtigen Ausgangsstand, damit nicht schon der Konflikt abweist.</summary>
        [TestMethod]
        public async Task AForeignAccountCannotChangeAJoker()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = await GegebenAsync(abend, Joker(abend, abend.MiaId));
            var hash = await HashAsync(abend.Gina, joker.Id);

            joker.PlayerId = abend.MaxId;
            var result = await PushAsync(abend.Mallory, new SyncPushRequest { Upserts = [SyncTestData.Changed(joker, hash)] });

            AssertForbidden(result, joker.Id, "Mallory hat Ginas Joker geändert.");
            Assert.AreEqual(hash, await HashAsync(abend.Gina, joker.Id), "Der Joker hat sich trotzdem geändert.");
        }

        [TestMethod]
        public async Task AForeignAccountCannotDeleteAJoker()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = await GegebenAsync(abend, Joker(abend, abend.MiaId));
            var hash = await HashAsync(abend.Gina, joker.Id);

            var result = await PushAsync(abend.Mallory, new SyncPushRequest
            {
                Deletes = [new SyncDelete { Type = nameof(PlayerJoker), Id = joker.Id, BaseHash = hash }],
            });

            AssertForbidden(result, joker.Id, "Mallory hat Ginas Joker gelöscht.");
            Assert.IsTrue((await SnapshotAsync(abend.Gina)).Rows.Any(r => r.Id == joker.Id), "Der Joker ist weg.");
        }

        [TestMethod]
        public async Task AJokerOnACellOfAnotherGameIsForbidden()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var anderes = SyncTestData.NewGame("Ginas zweites");
            await PushAsync(abend.Gina, anderes.AsNewPush());

            var joker = Joker(abend, abend.MiaId);
            joker.GameGridCoordinateId = anderes.Cell.Id;
            joker.RedeemedAt = DateTimeOffset.UtcNow;

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(joker)] });

            AssertForbidden(result, joker.Id, "Ein Joker hängt an der Zelle eines anderen Spiels - das Spiel ließe sich nie mehr löschen.");
        }

        [TestMethod]
        public async Task AJokerForSomeoneWhoDoesNotPlayIsForbidden()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var gast = Joker(abend, abend.GustavId);
            var fremd = Joker(abend, abend.MalloryId);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(gast), SyncTestData.New(fremd)] });

            AssertForbidden(result, gast.Id, "Ein Joker für einen Gast ohne Platz im Spiel wurde angenommen.");
            AssertForbidden(result, fremd.Id, "Ein Joker für ein fremdes Konto wurde angenommen.");
        }

        /// <summary>Platz und Joker im selben Push - so schickt es ein WPF, das offline vergeben hat.</summary>
        [TestMethod]
        public async Task ASeatInTheSamePushCounts()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var platz = new PlayerXGame { Id = Guid.NewGuid(), GameId = abend.Spiel.Game.Id, PlayerId = abend.GustavId };
            var joker = Joker(abend, abend.GustavId);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(platz), SyncTestData.New(joker)] });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
        }

        /// <summary>Mitspieler heißt: in DIESEM Spiel - ein Platz in einem anderen Spiel derselben Spielleiterin zählt nicht.</summary>
        [TestMethod]
        public async Task ASeatInAnotherGameDoesNotCount()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var zweites = SyncTestData.NewGame("Zweiter Abend");
            var push = zweites.AsNewPush();
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = zweites.Game.Id, PlayerId = abend.GustavId }));
            Assert.AreEqual(0, (await PushAsync(abend.Gina, push)).Rejected.Count, "Das zweite Spiel wurde nicht angelegt.");

            var amServer = Joker(abend, abend.GustavId);
            var imPush = Joker(abend, abend.MalloryId);
            var fremderPlatz = new PlayerXGame { Id = Guid.NewGuid(), GameId = zweites.Game.Id, PlayerId = abend.MalloryId };

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(fremderPlatz), SyncTestData.New(amServer), SyncTestData.New(imPush)],
            });

            AssertForbidden(result, amServer.Id, "Ein Platz in einem anderen Spiel (am Server) zählte als Mitspieler.");
            AssertForbidden(result, imPush.Id, "Ein Platz in einem anderen Spiel (im selben Push) zählte als Mitspieler.");
        }

        [TestMethod]
        public async Task ABlockAgainstSomeoneWhoDoesNotPlayIsForbidden()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var sperre = Joker(abend, abend.MiaId, JokerKind.Block);
            sperre.TargetPlayerId = abend.GustavId;

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(sperre)] });

            AssertForbidden(result, sperre.Id, "Eine Sperre gegen einen, der nicht mitspielt, wurde angenommen.");
        }

        /// <summary>Was das Einlösen verweigert, darf auch der Abgleich nicht schreiben (Befund aus C7b).</summary>
        [TestMethod]
        public async Task ABlockAgainstOneselfOrATargetOnAnotherKindIsForbidden()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var selbst = Joker(abend, abend.MiaId, JokerKind.Block);
            selbst.TargetPlayerId = abend.MiaId;
            var doppelt = Joker(abend, abend.MiaId, JokerKind.Double);
            doppelt.TargetPlayerId = abend.MaxId;
            var richtig = Joker(abend, abend.MiaId, JokerKind.Block);
            richtig.TargetPlayerId = abend.MaxId;

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(selbst), SyncTestData.New(doppelt), SyncTestData.New(richtig)],
            });

            AssertForbidden(result, selbst.Id, "Eine Sperre gegen sich selbst wurde angenommen.");
            AssertForbidden(result, doppelt.Id, "Ein Doppelt mit Ziel wurde angenommen.");
            Assert.IsFalse(result.Rejected.Any(r => r.Id == richtig.Id), "Die gewöhnliche Sperre gegen einen Mitspieler wurde abgewiesen.");
        }

        [TestMethod]
        public async Task AnUnknownKindIsRefused()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var joker = Joker(abend, abend.MiaId, (JokerKind)99);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(joker)] });

            Assert.IsTrue(result.Rejected.Any(r => r.Id == joker.Id), "Eine unbekannte Joker-Art wurde angenommen.");
        }

        /// <summary>Ein zu langer Grund kostet nur diese Zeile, nicht den ganzen Push.</summary>
        [TestMethod]
        public async Task AReasonOverTheLimitIsInvalidAndTheRestGoesThrough()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var lang = Joker(abend, abend.MiaId);
            lang.Reason = new string('x', PlayerJoker.MaxReasonLength + 1);
            var grenze = Joker(abend, abend.MaxId);
            grenze.Reason = new string('y', PlayerJoker.MaxReasonLength);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(lang), SyncTestData.New(grenze)] });

            Assert.IsTrue(result.Rejected.Any(r => r.Id == lang.Id && r.Reason == SyncRejectReason.Invalid), "Der zu lange Grund wurde nicht als ungültig abgewiesen.");
            Assert.IsTrue(result.Accepted.Any(a => a.Id == grenze.Id), "Ein Grund genau an der Grenze wurde nicht angenommen.");
        }

        [TestMethod]
        public async Task TheGameMasterCanDoAllOfIt()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var offen = Joker(abend, abend.MiaId);
            var sperre = Joker(abend, abend.MiaId, JokerKind.Block);
            sperre.TargetPlayerId = abend.MaxId;
            sperre.GameGridCoordinateId = abend.Spiel.Cell.Id;
            sperre.RedeemedAt = DateTimeOffset.UtcNow;

            var angelegt = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(offen), SyncTestData.New(sperre)] });
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            var hash = await HashAsync(abend.Gina, offen.Id);
            offen.Reason = "Doch für die Schätzfrage";
            var geaendert = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.Changed(offen, hash)] });
            Assert.AreEqual(0, geaendert.Rejected.Count, string.Join("; ", geaendert.Rejected.Select(r => r.Message)));

            var neuerHash = await HashAsync(abend.Gina, offen.Id);
            var geloescht = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [new SyncDelete { Type = nameof(PlayerJoker), Id = offen.Id, BaseHash = neuerHash }],
            });
            Assert.AreEqual(0, geloescht.Rejected.Count, string.Join("; ", geloescht.Rejected.Select(r => r.Message)));

            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsFalse(stand.Rows.Any(r => r.Id == offen.Id), "Der zurückgenommene Joker steht noch da.");
            Assert.IsTrue(stand.Rows.Any(r => r.Id == sperre.Id), "Die eingelöste Sperre fehlt.");
        }

        /// <summary>Joker hängen mit NO ACTION am Spiel - der Server nimmt sie beim Löschen selbst mit.</summary>
        [TestMethod]
        public async Task DeletingTheGameTakesItsJokersAlong()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var offen = await GegebenAsync(abend, Joker(abend, abend.MiaId));
            var eingeloest = Joker(abend, abend.MaxId, JokerKind.Shield);
            eingeloest.GameGridCoordinateId = abend.Spiel.Cell.Id;
            eingeloest.RedeemedAt = DateTimeOffset.UtcNow;
            await GegebenAsync(abend, eingeloest);

            var hash = await HashAsync(abend.Gina, abend.Spiel.Game.Id);
            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [new SyncDelete { Type = nameof(Game), Id = abend.Spiel.Game.Id, BaseHash = hash }],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsFalse(stand.Rows.Any(r => r.Id == offen.Id || r.Id == eingeloest.Id), "Joker des gelöschten Spiels stehen noch da.");
        }
    }
}
