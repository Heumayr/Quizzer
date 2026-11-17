using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    [TestClass]
    public sealed class SyncTests
    {
        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<SyncSnapshot> SnapshotAsync(HttpClient client)
            => (await client.GetFromJsonAsync<SyncSnapshot>("/api/v1/sync/snapshot"))!;

        [TestMethod]
        public async Task ANewGameBelongsToItsCreatorWhateverTheClientClaims()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, aliceTokens) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (_, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "Bob");

            // Alice behauptet, Bob sei der Gamemaster.
            var set = SyncTestData.NewGame("Behauptung", claimedGameMaster: bobTokens.Account.PlayerId);
            var result = await PushAsync(alice, set.AsNewPush());

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            Assert.AreEqual(4, result.Accepted.Count);

            var snapshot = await SnapshotAsync(alice);
            var game = snapshot.Rows.Single(r => r.Id == set.Game.Id);
            Assert.AreEqual(aliceTokens.Account.PlayerId, game.Values["ModeratorPlayerId"].GetGuid());

            var question = snapshot.Rows.Single(r => r.Id == set.Question.Id);
            Assert.AreEqual(aliceTokens.Account.PlayerId, question.Values["OwnerPlayerId"].GetGuid());
        }

        [TestMethod]
        public async Task OthersSeeNeitherThePrivateGameNorItsQuestion()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (bob, _) = await ServerTestHost.NewMemberAsync(factory, "Bob");

            var set = SyncTestData.NewGame("Privat");
            await PushAsync(alice, set.AsNewPush());

            var snapshot = await SnapshotAsync(bob);
            Assert.IsFalse(snapshot.Rows.Any(r => r.Id == set.Game.Id), "Bob sieht Alices Spiel.");
            Assert.IsFalse(snapshot.Rows.Any(r => r.Id == set.Question.Id), "Bob sieht Alices Frage.");
            Assert.IsFalse(snapshot.Rows.Any(r => r.Id == set.Cell.Id), "Bob sieht Alices Zelle.");
        }

        [TestMethod]
        public async Task AFellowPlayerSeesTheGameButCannotChangeIt()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "Bob");

            var set = SyncTestData.NewGame("Gemeinsam");
            var push = set.AsNewPush();
            var seat = new PlayerXGame { Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = bobTokens.Account.PlayerId };
            push.Upserts.Add(SyncTestData.New(seat));
            await PushAsync(alice, push);

            var snapshot = await SnapshotAsync(bob);
            var gameRow = snapshot.Rows.Single(r => r.Id == set.Game.Id);
            Assert.IsTrue(snapshot.Rows.Any(r => r.Id == set.Question.Id), "Die Frage im Spiel muss mitkommen.");

            // Bob versucht, das Spiel umzubenennen und die Frage zu aendern.
            set.Game.Designation = "Von Bob umbenannt";
            set.Game.ModeratorPlayerId = snapshot.Rows.Single(r => r.Id == set.Game.Id).Values["ModeratorPlayerId"].GetGuid();
            set.Question.QuestionText = "Von Bob geaendert";
            set.Question.OwnerPlayerId = snapshot.Rows.Single(r => r.Id == set.Question.Id).Values["OwnerPlayerId"].GetGuid();

            var questionRow = snapshot.Rows.Single(r => r.Id == set.Question.Id);
            var result = await PushAsync(bob, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(set.Game, gameRow.Hash), SyncTestData.Changed(set.Question, questionRow.Hash)],
            });

            Assert.AreEqual(2, result.Rejected.Count);
            Assert.IsTrue(result.Rejected.All(r => r.Reason == SyncRejectReason.Forbidden));
            Assert.AreEqual(0, result.Accepted.Count);

            var after = await SnapshotAsync(alice);
            Assert.AreEqual("Spiel Gemeinsam", after.Rows.Single(r => r.Id == set.Game.Id).Values["Designation"].GetString());
        }

        [TestMethod]
        public async Task AStaleBaseIsAConflictNotASilentOverwrite()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, aliceTokens) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var set = SyncTestData.NewGame("Konflikt");
            await PushAsync(alice, set.AsNewPush());
            set.Game.ModeratorPlayerId = aliceTokens.Account.PlayerId;

            var original = (await SnapshotAsync(alice)).Rows.Single(r => r.Id == set.Game.Id);

            // Erster Rechner aendert ...
            set.Game.Designation = "Vom PC";
            var first = await PushAsync(alice, new SyncPushRequest { Upserts = [SyncTestData.Changed(set.Game, original.Hash)] });
            Assert.AreEqual(1, first.Accepted.Count);

            // ... der zweite baut noch auf dem alten Stand auf.
            set.Game.Designation = "Vom Laptop";
            var second = await PushAsync(alice, new SyncPushRequest { Upserts = [SyncTestData.Changed(set.Game, original.Hash)] });

            Assert.AreEqual(1, second.Rejected.Count);
            Assert.AreEqual(SyncRejectReason.Conflict, second.Rejected[0].Reason);
            Assert.AreEqual("Am Server inzwischen geändert.", second.Rejected[0].Message, "Ein echter Stand-Konflikt heisst anders (V12).");
            Assert.AreEqual("Vom PC", (await SnapshotAsync(alice)).Rows.Single(r => r.Id == set.Game.Id).Values["Designation"].GetString());
        }

        [TestMethod]
        public async Task TheSnapshotCarriesNoPasswordHashAndNoAccounts()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var snapshot = await SnapshotAsync(alice);

            Assert.IsTrue(snapshot.Rows.Any(r => r.Type == nameof(Player)));
            Assert.IsFalse(snapshot.Rows.Any(r => r.Values.ContainsKey("PasswordHash")));
            Assert.IsFalse(snapshot.Rows.Any(r => r.Type is "Account" or "RefreshToken"));
        }

        [TestMethod]
        public async Task UnknownTypesAndFieldsAreRejectedAsInvalid()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var account = new SyncRow { Type = "Account", Id = Guid.NewGuid() };
            var category = SyncTestData.Row(new Category { Id = Guid.NewGuid(), Designation = "x" });
            category.Values["PasswordHash"] = System.Text.Json.JsonSerializer.SerializeToElement("hack");

            var result = await PushAsync(alice, new SyncPushRequest
            {
                Upserts = [new SyncUpsert { Row = account }, new SyncUpsert { Row = category }],
            });

            Assert.AreEqual(2, result.Rejected.Count);
            Assert.IsTrue(result.Rejected.All(r => r.Reason == SyncRejectReason.Invalid));
        }

        [TestMethod]
        public async Task DeletingAGameTakesItsCellsAlong()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, _) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var set = SyncTestData.NewGame("Loeschen");
            await PushAsync(alice, set.AsNewPush());

            var game = (await SnapshotAsync(alice)).Rows.Single(r => r.Id == set.Game.Id);
            var result = await PushAsync(alice, new SyncPushRequest
            {
                Deletes = [new SyncDelete { Type = game.Type, Id = game.Id, BaseHash = game.Hash }],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var after = await SnapshotAsync(alice);
            Assert.IsFalse(after.Rows.Any(r => r.Id == set.Game.Id || r.Id == set.Cell.Id));
            Assert.IsTrue(after.Rows.Any(r => r.Id == set.Question.Id), "Die Frage bleibt, nur das Spiel geht.");
        }

        [TestMethod]
        public async Task StatisticsCountTheGamesOfTheAccount()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (alice, aliceTokens) = await ServerTestHost.NewMemberAsync(factory, "Alice");

            var set = SyncTestData.NewGame("Statistik");
            var push = set.AsNewPush();
            var seat = new PlayerXGame { Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = aliceTokens.Account.PlayerId };
            var result = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = aliceTokens.Account.PlayerId,
                QuestionBaseId = set.Question.Id, GameGridCoordinateId = set.Cell.Id, Score = 100, RightCount = 1,
            };
            push.Upserts.Add(SyncTestData.New(seat));
            push.Upserts.Add(SyncTestData.New(result));
            Assert.AreEqual(0, (await PushAsync(alice, push)).Rejected.Count);

            var stats = (await alice.GetFromJsonAsync<StatisticsResponse>("/api/v1/me/statistics"))!;
            var game = stats.Games.Single(g => g.GameId == set.Game.Id);

            Assert.AreEqual(100, game.FinalScore);
            Assert.AreEqual(1, game.Rank);
            Assert.IsTrue(game.IsGameMaster);
        }

        /// <summary>
        /// M35 C9, A01: die Bestenliste rechnet ueber dieselben Spiele wie die Statistik. Ein Rekord aus einem Spiel, in dem
        /// das Konto weder leitet noch mitspielt, steht nirgends - weder als Platz noch als Klau; mit Platz im Spiel schon.
        /// Steht hier und nicht bei den Bestenliste-Tests: jedes Konto sieht alle Mitspieler, und <c>ClientSafetyTests</c>
        /// rechnet mit wenigen davon (laeuft davor).
        /// </summary>
        [TestMethod]
        public async Task TheLeaderboardShowsARecordOnlyOnceTheAccountPlaysInItsGame()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, ginaTokens) = await ServerTestHost.NewMemberAsync(factory, "GinaRekord");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobRekord");
            var ginaId = ginaTokens.Account.PlayerId;

            var set = SyncTestData.NewGame("Rekordabend");
            var reaktion = new Quizzer.DataModels.Models.QuestionTypes.MinigameQuestion
            {
                Id = Guid.NewGuid(), Designation = "Reaktion", CategoryId = set.Category.Id, Points = 100,
            };
            Quizzer.DataModels.Questions.Minispiele.MinigameProfiles.For(Quizzer.DataModels.Enumerations.MinigameKind.Reaction).ApplyDefaults(reaktion);
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = set.Game.Id, QuestionBaseId = reaktion.Id, X = 1, Y = 0 };
            var rekord = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = ginaId, QuestionBaseId = reaktion.Id, GameGridCoordinateId = zelle.Id,
                Score = 100, RightCount = 1, RecordValue = 187, RecordedAt = DateTimeOffset.UtcNow, Steals = 2,
            };
            var push = set.AsNewPush();
            push.Upserts.AddRange([SyncTestData.New(reaktion), SyncTestData.New(zelle),
                SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = ginaId }), SyncTestData.New(rekord)]);
            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            static async Task<Leaderboard> BestenlisteAsync(HttpClient client)
                => (await client.GetFromJsonAsync<StatisticsResponse>("/api/v1/me/statistics"))!.Leaderboard;

            var fremd = await BestenlisteAsync(bob);
            Assert.AreEqual(0, fremd.Categories.Sum(c => c.Entries.Count), "Bob sieht einen Platz aus Ginas Spiel.");
            Assert.AreEqual(0, fremd.MostSteals.Count, "Bob sieht Klaus aus Ginas Spiel.");

            var platz = await PushAsync(gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = bobTokens.Account.PlayerId })],
            });
            Assert.AreEqual(0, platz.Rejected.Count, string.Join("; ", platz.Rejected.Select(r => r.Message)));

            var mitspieler = await BestenlisteAsync(bob);
            var eintrag = mitspieler.Categories.Single(c => c.Kind == Quizzer.DataModels.Enumerations.MinigameKind.Reaction).Entries.Single();
            Assert.AreEqual((ginaId, 187, "Spiel Rekordabend"), (eintrag.PlayerId, eintrag.Value, eintrag.GameName),
                "Mit Platz im Spiel fehlt der Rekord - die Kontrolle misst sonst nichts.");
            Assert.AreEqual((ginaId, 2), (mitspieler.MostSteals.Single().PlayerId, mitspieler.MostSteals.Single().Steals));
        }
    }
}
