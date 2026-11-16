using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Sync;
using System.Net;
using System.Net.Http.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Bestenliste am Server (M35 C9): Rohwert und Klaus am Ergebnis sind Vorschlag des Clients - ausserhalb des Bereichs
    /// ihrer Art je Zeile <c>Invalid</c>, der Rest des Pushes geht durch (A06/A08).
    /// </summary>
    [TestClass]
    public sealed partial class BestenlisteTests
    {
        /// <summary>
        /// Gina leitet und spielt; je eine Zelle Standard, drei Zellen Reaktion und zwei Mehrheit. Ohne Gaeste: Mitspieler und
        /// Kategorien sieht jedes Konto, und <c>ClientSafetyTests</c> rechnet mit wenigen davon.
        /// </summary>
        private sealed record Abend(HttpClient Gina, Guid GinaId, SyncTestData.GameSet Spiel,
            IReadOnlyList<GameGridCoordinate> Reaktion, IReadOnlyList<GameGridCoordinate> Mehrheit);

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
        {
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static MinigameQuestion Minispiel(MinigameKind art, Guid kategorie)
        {
            var frage = new MinigameQuestion { Id = Guid.NewGuid(), Designation = art.ToString(), CategoryId = kategorie, Points = 100 };
            MinigameProfiles.For(art).ApplyDefaults(frage);
            return frage;
        }

        private static List<GameGridCoordinate> Zellen(SyncTestData.GameSet spiel, QuestionBase frage, int zeile, int anzahl)
            => Enumerable.Range(0, anzahl)
                .Select(x => new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Game.Id, QuestionBaseId = frage.Id, X = x, Y = zeile })
                .ToList();

        private static async Task<Abend> AbendAsync(WebApplicationFactory<Program> factory, string name = "Bestenliste")
        {
            var (gina, ginaTokens) = await ServerTestHost.NewMemberAsync(factory, "Gina" + name);

            var spiel = SyncTestData.NewGame(name);
            var reaktion = Minispiel(MinigameKind.Reaction, spiel.Category.Id);
            var mehrheit = Minispiel(MinigameKind.Majority, spiel.Category.Id);
            var reaktionsZellen = Zellen(spiel, reaktion, 1, 3);
            var mehrheitsZellen = Zellen(spiel, mehrheit, 2, 2);

            var push = spiel.AsNewPush();
            push.Upserts.AddRange([SyncTestData.New(reaktion), SyncTestData.New(mehrheit)]);
            push.Upserts.AddRange(reaktionsZellen.Concat(mehrheitsZellen).Select(z => SyncTestData.New(z)));
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = ginaTokens.Account.PlayerId }));

            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            return new Abend(gina, ginaTokens.Account.PlayerId, spiel, reaktionsZellen, mehrheitsZellen);
        }

        private static QuestionResult Ergebnis(Abend abend, GameGridCoordinate zelle, int? rohwert, int klaus = 0) => new()
        {
            Id = Guid.NewGuid(),
            GameId = abend.Spiel.Game.Id,
            GameGridCoordinateId = zelle.Id,
            QuestionBaseId = zelle.QuestionBaseId ?? throw new InvalidOperationException("Zelle ohne Frage."),
            PlayerId = abend.GinaId,
            Score = 100,
            RightCount = 1,
            RecordValue = rohwert,
            RecordedAt = rohwert == null ? null : DateTimeOffset.UtcNow,
            Steals = klaus,
        };

        private static void AssertInvalid(SyncPushResponse result, QuestionResult zeile, string warum)
            => Assert.IsTrue(result.Rejected.Any(r => r.Id == zeile.Id && r.Reason == SyncRejectReason.Invalid), warum);

        private static void AssertAccepted(SyncPushResponse result, QuestionResult zeile, string warum)
            => Assert.IsTrue(result.Accepted.Any(a => a.Id == zeile.Id), warum + " " + string.Join("; ", result.Rejected.Select(r => r.Message)));

        [TestMethod]
        public async Task ARecordOutsideTheRangeOfItsKindIsInvalidAndTheRestGoesThrough()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var record = MinigameProfiles.For(MinigameKind.Reaction).Record!;

            var zuSchnell = Ergebnis(abend, abend.Reaktion[0], record.Min - 1);
            var zuLangsam = Ergebnis(abend, abend.Reaktion[1], record.Max + 1);
            var gueltig = Ergebnis(abend, abend.Reaktion[2], record.Min, klaus: 1);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(zuSchnell), SyncTestData.New(zuLangsam), SyncTestData.New(gueltig)],
            });

            AssertInvalid(result, zuSchnell, "Eine Reaktion unter der Fruehstartgrenze wurde angenommen.");
            AssertInvalid(result, zuLangsam, "Eine Reaktion ueber der Wertungsgrenze wurde angenommen.");
            AssertAccepted(result, gueltig, "Ein Wert genau an der Grenze wurde nicht angenommen.");
        }

        [TestMethod]
        public async Task ARecordAtAQuestionWhoseKindHasNoneIsInvalid()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var standard = Ergebnis(abend, abend.Spiel.Cell, 250);
            var mehrheit = Ergebnis(abend, abend.Mehrheit[0], 250);
            var ohneRohwert = Ergebnis(abend, abend.Mehrheit[1], null, klaus: 2);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(standard), SyncTestData.New(mehrheit), SyncTestData.New(ohneRohwert)],
            });

            AssertInvalid(result, standard, "Ein Rohwert an einer Standardfrage wurde angenommen.");
            AssertInvalid(result, mehrheit, "Ein Rohwert an einer Mehrheit (Art ohne Bestwert) wurde angenommen.");
            AssertAccepted(result, ohneRohwert, "Klaus ohne Rohwert gehoeren an jede Frage (Kopfgeld).");
        }

        [TestMethod]
        public async Task NegativeStealsAreInvalid()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var negativ = Ergebnis(abend, abend.Spiel.Cell, null, klaus: -1);
            var ohneKlau = Ergebnis(abend, abend.Reaktion[0], null, klaus: 0);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(negativ), SyncTestData.New(ohneKlau)] });

            AssertInvalid(result, negativ, "Negative Klaus wurden angenommen.");
            AssertAccepted(result, ohneKlau, "Ein Ergebnis ohne Klau wurde abgewiesen.");
        }

        /// <summary>Die Art kommt auch aus demselben Push - eine neue Frage ist am Server noch nicht da.</summary>
        [TestMethod]
        public async Task TheKindIsReadFromAQuestionInTheSamePush()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var neu = Minispiel(MinigameKind.Reaction, abend.Spiel.Category.Id);
            var zellen = Zellen(abend.Spiel, neu, 3, 2);

            var zuSchnell = Ergebnis(abend, zellen[0], 50);
            var gueltig = Ergebnis(abend, zellen[1], 250);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.New(neu), SyncTestData.New(zellen[0]), SyncTestData.New(zellen[1]), SyncTestData.New(zuSchnell), SyncTestData.New(gueltig)],
            });

            AssertInvalid(result, zuSchnell, "Die Art der Frage im selben Push wurde nicht gelesen.");
            AssertAccepted(result, gueltig, "Ein gueltiger Rohwert an einer neuen Frage wurde abgewiesen.");
        }
    }
}
