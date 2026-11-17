using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Teams am Server (M37 T1, Entwurf Abschnitt 2): schreiben darf nur der Gamemaster, Zuteilung und Ergebnis zeigen nur auf
    /// ein Team desselben Spiels, die Grenzen kosten je Zeile (<c>Invalid</c>). Zusammenhang gilt auch für den Admin.
    /// Muster: <see cref="JokerTests"/>.
    /// </summary>
    [TestClass]
    public sealed partial class TeamTests
    {
        /// <summary>Gina leitet A (Mia mit Konto und Max als Gast sitzen, Team Rot); Bob leitet B mit Team Blau; Mallory ist fremd.</summary>
        private sealed record Abend(
            HttpClient Gina, HttpClient Mia, HttpClient Mallory, HttpClient Bob, HttpClient Admin,
            SyncTestData.GameSet A, PlayerXGame MiaInA, PlayerXGame MaxInA, GameTeam Rot, GameTeam BobsBlau);

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

        private static void AssertRejected(SyncPushResponse result, Guid id, SyncRejectReason grund, string warum)
            => Assert.IsTrue(result.Rejected.Any(r => r.Id == id && r.Reason == grund),
                warum + " Abgewiesen: " + string.Join("; ", result.Rejected.Select(r => $"{r.Id} {r.Reason} {r.Message}")));

        private static void AssertAccepted(SyncPushResponse result, Guid id, string warum)
            => Assert.IsTrue(result.Accepted.Any(a => a.Id == id), warum + " " + string.Join("; ", result.Rejected.Select(r => r.Message)));

        private static GameTeam Team(Guid spiel, string name, int farbe = 0) => new() { Id = Guid.NewGuid(), GameId = spiel, Name = name, ColorIndex = farbe };

        private static async Task<Abend> AbendAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var (mia, miaTokens) = await ServerTestHost.NewMemberAsync(factory, "Mia");
            var (mallory, _) = await ServerTestHost.NewMemberAsync(factory, "Mallory");
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");
            var admin = ServerTestHost.Authorized(factory,
                await ServerTestHost.LoginAsync(factory.CreateClient(), ServerTestHost.AdminUser, ServerTestHost.AdminPassword));

            var a = SyncTestData.NewGame("Teamabend");
            a.Game.IsTeamGame = true;
            var max = new Player { Id = Guid.NewGuid(), Designation = "Max", DisplayName = "Max" };
            var rot = Team(a.Game.Id, "Rot", 5);
            var miaInA = new PlayerXGame { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = miaTokens.Account.PlayerId, GameTeamId = rot.Id };
            var maxInA = new PlayerXGame { Id = Guid.NewGuid(), GameId = a.Game.Id, PlayerId = max.Id };

            var push = a.AsNewPush();
            push.Upserts.AddRange([SyncTestData.New(max), SyncTestData.New(rot), SyncTestData.New(miaInA), SyncTestData.New(maxInA)]);
            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            var b = SyncTestData.NewGame("Bobs Abend");
            var blau = Team(b.Game.Id, "Blau", 4);
            var bobsPush = b.AsNewPush();
            bobsPush.Upserts.Add(SyncTestData.New(blau));
            Assert.AreEqual(0, (await PushAsync(bob, bobsPush)).Rejected.Count, "Bobs Spiel samt Team wurde nicht angelegt.");

            return new Abend(gina, mia, mallory, bob, admin, a, miaInA, maxInA, rot, blau);
        }

        [TestMethod]
        public async Task AForeignAccountDoesNotSeeTheTeamsButAMemberDoes()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            Assert.IsFalse((await SnapshotAsync(abend.Mallory)).Rows.Any(r => r.Id == abend.Rot.Id), "Mallory sieht ein Team eines fremden Spiels.");
            Assert.IsTrue((await SnapshotAsync(abend.Mia)).Rows.Any(r => r.Id == abend.Rot.Id), "Die Mitspielerin sieht das Team ihres Spiels nicht.");
        }

        /// <summary>Fremdes Konto: anlegen und umbenennen - mit dem richtigen Ausgangsstand, damit nicht schon der Konflikt abweist.</summary>
        [TestMethod]
        public async Task AForeignAccountCannotCreateOrRenameATeam()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var neu = Team(abend.A.Game.Id, "Mallorys");
            var hash = await HashAsync(abend.Gina, abend.Rot.Id);
            abend.Rot.Name = "Gekapert";

            var result = await PushAsync(abend.Mallory, new SyncPushRequest { Upserts = [SyncTestData.New(neu), SyncTestData.Changed(abend.Rot, hash)] });

            AssertRejected(result, neu.Id, SyncRejectReason.Forbidden, "Mallory hat in Ginas Spiel ein Team angelegt.");
            AssertRejected(result, abend.Rot.Id, SyncRejectReason.Forbidden, "Mallory hat Ginas Team umbenannt.");
            Assert.AreEqual(hash, await HashAsync(abend.Gina, abend.Rot.Id), "Das Team hat sich trotzdem geändert.");
        }

        /// <summary>Mitspielen heißt nicht leiten: Mia legt kein Team an, benennt keines um und teilt sich nicht selbst zu.</summary>
        [TestMethod]
        public async Task AMemberWhoIsNotTheGameMasterCannotWriteTeams()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var neu = Team(abend.A.Game.Id, "Mias");
            var teamHash = await HashAsync(abend.Gina, abend.Rot.Id);
            var platzHash = await HashAsync(abend.Gina, abend.MiaInA.Id);
            abend.Rot.Name = "Mias Rot";
            abend.MiaInA.GameTeamId = null;

            var result = await PushAsync(abend.Mia, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(neu), SyncTestData.Changed(abend.Rot, teamHash), SyncTestData.Changed(abend.MiaInA, platzHash)],
            });

            AssertRejected(result, neu.Id, SyncRejectReason.Forbidden, "Mia hat ein Team angelegt.");
            AssertRejected(result, abend.Rot.Id, SyncRejectReason.Forbidden, "Mia hat das Team umbenannt.");
            AssertRejected(result, abend.MiaInA.Id, SyncRejectReason.Forbidden, "Mia hat ihre eigene Zuteilung geändert.");
        }

        /// <summary>
        /// Ein Team aus Bobs Spiel und eines, das es nicht gibt: beide abgewiesen, mit derselben Meldung - sonst verriete der
        /// Server, welche fremden Teams es gibt. Gilt auch für den Admin (V09). Die richtige Zuteilung geht durch.
        /// </summary>
        [TestMethod]
        public Task ATeamOfAnotherGameIsRefusedLikeAMissingOne() => TeamOfAnotherGameAsync(admin: false);

        [TestMethod]
        public Task TheAdminCannotAssignATeamOfAnotherGameEither() => TeamOfAnotherGameAsync(admin: true);

        private static async Task TeamOfAnotherGameAsync(bool admin)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var maxHash = await HashAsync(abend.Gina, abend.MaxInA.Id);
            var miaHash = await HashAsync(abend.Gina, abend.MiaInA.Id);
            abend.MaxInA.GameTeamId = abend.BobsBlau.Id;
            abend.MiaInA.GameTeamId = Guid.NewGuid();
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = abend.A.Game.Id, GameGridCoordinateId = abend.A.Cell.Id, QuestionBaseId = abend.A.Question.Id,
                PlayerId = abend.MiaInA.PlayerId, Score = 100, GameTeamId = abend.BobsBlau.Id,
            };
            var richtig = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = abend.A.Game.Id, GameGridCoordinateId = abend.A.Cell.Id, QuestionBaseId = abend.A.Question.Id,
                PlayerId = abend.MaxInA.PlayerId, Score = 100, GameTeamId = abend.Rot.Id,
            };

            var result = await PushAsync(admin ? abend.Admin : abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(abend.MaxInA, maxHash), SyncTestData.Changed(abend.MiaInA, miaHash), SyncTestData.New(ergebnis), SyncTestData.New(richtig)],
            });

            AssertRejected(result, abend.MaxInA.Id, SyncRejectReason.Forbidden, "Max wurde einem Team aus Bobs Spiel zugeteilt.");
            AssertRejected(result, abend.MiaInA.Id, SyncRejectReason.Forbidden, "Mia wurde einem Team zugeteilt, das es nicht gibt.");
            AssertRejected(result, ergebnis.Id, SyncRejectReason.Forbidden, "Ein Ergebnis trägt den Stempel eines Teams aus Bobs Spiel.");
            AssertAccepted(result, richtig.Id, "Ein Ergebnis mit dem Stempel eines eigenen Teams wurde abgewiesen.");

            var meldungen = result.Rejected.Select(r => r.Message).Distinct().ToList();
            Assert.AreEqual(1, meldungen.Count, "Die Meldungen unterscheiden „anderes Spiel“ von „gibt es nicht“: " + string.Join(" | ", meldungen));
        }

        /// <summary>Jede Grenze kostet nur ihre Zeile (A08); die Grenzwerte selbst gehen durch.</summary>
        [TestMethod]
        public async Task TeamRowsOutsideTheLimitsAreInvalidOneByOne()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var spiel = abend.A.Game.Id;
            var ungueltig = new[]
            {
                Team(spiel, new string('X', GameTeam.MaxNameLength + 1)), Team(spiel, ""), Team(spiel, "   "),
                Team(spiel, "Farbe", -1), Team(spiel, "Farbe", 8), new GameTeam { Id = Guid.NewGuid(), GameId = spiel, Name = "Sprecher", SpeakerTurn = -1 },
            };
            var grenzwerte = new[] { Team(spiel, new string('A', GameTeam.MaxNameLength), 7), Team(spiel, "Null") };

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [.. ungueltig.Concat(grenzwerte).Select(SyncTestData.New)] });

            foreach (var team in ungueltig)
                AssertRejected(result, team.Id, SyncRejectReason.Invalid, $"Das Team „{team.Name}“ (Farbe {team.ColorIndex}, Sprecher {team.SpeakerTurn}) kam durch.");

            foreach (var team in grenzwerte)
                AssertAccepted(result, team.Id, $"Das Team „{team.Name}“ an der Grenze wurde abgewiesen.");
        }

        [TestMethod]
        public async Task TeamSettingsOutsideTheLimitsAreInvalid()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            Game Spiel(TeamMinigameScoring wertung, int sekunden) => new()
            {
                Id = Guid.NewGuid(), Designation = "Einstellung", IsTeamGame = true, TeamMinigameScoring = wertung, TeamCouncilSeconds = sekunden,
            };
            var ungueltig = new[] { Spiel((TeamMinigameScoring)2, 30), Spiel(TeamMinigameScoring.Sum, -1), Spiel(TeamMinigameScoring.Sum, 601) };
            var grenzwerte = new[] { Spiel(TeamMinigameScoring.Sum, 0), Spiel(TeamMinigameScoring.Average, 600) };

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [.. ungueltig.Concat(grenzwerte).Select(SyncTestData.New)] });

            foreach (var spiel in ungueltig)
                AssertRejected(result, spiel.Id, SyncRejectReason.Invalid, $"Wertung {(int)spiel.TeamMinigameScoring}, Beratung {spiel.TeamCouncilSeconds} s kam durch.");

            foreach (var spiel in grenzwerte)
                AssertAccepted(result, spiel.Id, $"Beratung {spiel.TeamCouncilSeconds} s an der Grenze wurde abgewiesen.");
        }
    }
}
