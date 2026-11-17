using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Statistics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Die Bestenliste ohne Server (M37 E03): was der PC abgeholt hat, ergibt lokal gerechnet dieselbe Liste wie am Server.
    /// Eigene Klasse hinter <c>ClientSafetyTests</c>: jedes Konto sieht alle Mitspieler, und die Klasse rechnet mit wenigen.
    /// </summary>
    [TestClass]
    public sealed class LokaleBestenlisteTests
    {
        private const string Kennwort = "Mitglieds-Kennwort-123";

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

            var result = (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            return result;
        }

        private static MinigameQuestion Minispiel(MinigameKind art, Guid kategorie)
        {
            var frage = new MinigameQuestion { Id = Guid.NewGuid(), Designation = "Lokal " + art, CategoryId = kategorie, Points = 100 };
            MinigameProfiles.For(art).ApplyDefaults(frage);
            return frage;
        }

        private static GameGridCoordinate Zelle(Game spiel, QuestionBase frage, int zeile)
            => new() { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = frage.Id, X = 0, Y = zeile, IsDone = true };

        private static QuestionResult Ergebnis(GameGridCoordinate zelle, Guid spieler, int? rohwert, int klaus = 0) => new()
        {
            Id = Guid.NewGuid(), GameId = zelle.GameId, GameGridCoordinateId = zelle.Id, PlayerId = spieler,
            QuestionBaseId = zelle.QuestionBaseId ?? throw new InvalidOperationException("Zelle ohne Frage."),
            Score = 100, RightCount = 1, RecordValue = rohwert, RecordedAt = rohwert == null ? null : DateTimeOffset.UtcNow, Steals = klaus,
        };

        /// <summary>Ein Abend mit diesen Minispielen und einem Platz je Spieler; die Ergebnisse baut der Aufrufer.</summary>
        private static (SyncPushRequest Push, Dictionary<MinigameKind, GameGridCoordinate> Zellen, SyncTestData.GameSet Spiel) Abend(
            string name, MinigameKind[] arten, params Guid[] spieler)
        {
            var spiel = SyncTestData.NewGame(name);
            var push = spiel.AsNewPush();
            var zellen = new Dictionary<MinigameKind, GameGridCoordinate>();

            foreach (var (art, zeile) in arten.Select((a, i) => (a, i + 1)))
            {
                var frage = Minispiel(art, spiel.Category.Id);
                zellen[art] = Zelle(spiel.Game, frage, zeile);
                push.Upserts.AddRange([SyncTestData.New(frage), SyncTestData.New(zellen[art])]);
            }

            push.Upserts.AddRange(spieler.Select(s => SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = s })));
            return (push, zellen, spiel);
        }

        /// <summary>
        /// Gina leitet einen Abend mit Bob, Bob einen ohne Gina mit dem besten Wert von allen. Ginas PC holt ab und rechnet
        /// lokal: dieselbe Liste wie am Server, Bobs fremder Abend fehlt auf beiden Seiten, Klaus an der Standardfrage zaehlen.
        /// </summary>
        [TestMethod]
        public async Task TheLocalLeaderboardEqualsTheServersForTheSameRows()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, ginaTokens) = await ServerTestHost.NewMemberAsync(factory, "GinaLokal");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobLokal");
            var ginaId = ginaTokens.Account.PlayerId;
            var bobId = bobTokens.Account.PlayerId;

            var mitBob = Abend("LokalGina", [MinigameKind.Reaction, MinigameKind.TapSprint], ginaId, bobId);
            mitBob.Push.Upserts.AddRange(new[]
            {
                Ergebnis(mitBob.Zellen[MinigameKind.Reaction], ginaId, 210),
                Ergebnis(mitBob.Zellen[MinigameKind.Reaction], bobId, 190),
                Ergebnis(mitBob.Zellen[MinigameKind.TapSprint], ginaId, 650),
                Ergebnis(mitBob.Spiel.Cell, bobId, null, klaus: 3),
            }.Select(e => SyncTestData.New(e)));
            await PushAsync(gina, mitBob.Push);

            var ohneGina = Abend("LokalBob", [MinigameKind.Reaction], bobId);
            ohneGina.Push.Upserts.Add(SyncTestData.New(Ergebnis(ohneGina.Zellen[MinigameKind.Reaction], bobId, 160, klaus: 5)));
            await PushAsync(bob, ohneGina.Push);

            await using var pc = await ClientHarness.CreateAsync(factory, "Bestenliste");
            await pc.Api.LoginAsync(ginaTokens.Account.UserName, Kennwort);
            await pc.Sync.PullAsync();

            var amServer = (await gina.GetFromJsonAsync<StatisticsResponse>("/api/v1/me/statistics", QuizzerApiClient.Json))!.Leaderboard;
            Leaderboard lokal;
            await using (var context = pc.Local())
                lokal = await LeaderboardRows.CalculateAsync(context, gameIds: null);

            var reaktion = amServer.Categories.Single(c => c.Kind == MinigameKind.Reaction).Entries;
            CollectionAssert.AreEqual(new[] { (bobId, 190), (ginaId, 210) }, reaktion.Select(e => (e.PlayerId, e.Value)).ToList(),
                "Kontrolle: am Server steht nicht Ginas Abend allein - der Vergleich maesse sonst nichts.");
            Assert.AreEqual((bobId, 3), (amServer.MostSteals.Single().PlayerId, amServer.MostSteals.Single().Steals));

            Assert.AreEqual(JsonSerializer.Serialize(amServer, QuizzerApiClient.Json), JsonSerializer.Serialize(lokal, QuizzerApiClient.Json),
                "Lokal gerechnet steht eine andere Liste als am Server.");
        }
    }
}
