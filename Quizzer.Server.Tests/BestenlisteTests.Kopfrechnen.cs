using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Server;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    public sealed partial class BestenlisteTests
    {
        /// <summary>Die Antwort der Statistik, wie ein WPF vom Stand <c>dc2a20c</c> (vor M37 E05) sie liest: ohne Spielweise.</summary>
        private sealed record AlteStatistik(Guid PlayerId, string PlayerName, int GamesPlayed, int GamesWon, int TotalScore,
            int GamesAsGameMaster, IReadOnlyList<GameStatistic> Games, AlteBestenliste Leaderboard);

        private sealed record AlteBestenliste(IReadOnlyList<AlteListe> Categories, IReadOnlyList<StealsEntry> MostSteals);

        private sealed record AlteListe(
            MinigameKind Kind, string Title, string KindName, string Unit, bool LowerIsBetter, IReadOnlyList<LeaderboardEntry> Entries);

        /// <summary>
        /// M37 E05: Kopfrechnen leicht, mittel und schwer - je Schwierigkeit eine Liste, die Schwierigkeit liest der Server aus
        /// seiner Frage. Ein WPF vom Stand davor liest dieselbe Antwort (Rundlauf ueber den echten Server) und zeigt drei
        /// unterscheidbare Kacheln, je mit ihrem Wert. Die Rohwerte laufen der Schwierigkeit entgegen: gemischt stuende leicht
        /// ueberall vorn.
        /// </summary>
        [TestMethod]
        public async Task MentalArithmeticIsListedPerDifficultyAndAnOlderProgramStillReadsIt()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, ginaTokens) = await ServerTestHost.NewMemberAsync(factory, "GinaKopfrechnen");
            var ginaId = ginaTokens.Account.PlayerId;

            var spiel = SyncTestData.NewGame("Kopfrechnen");
            var push = spiel.AsNewPush();
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = ginaId }));

            var werte = new[] { (KopfrechnenAufgaben.Leicht, 1800), (KopfrechnenAufgaben.Mittel, 900), (KopfrechnenAufgaben.Schwer, 300) };
            foreach (var (stufe, wert) in werte)
            {
                var frage = Minispiel(MinigameKind.MentalArithmetic, spiel.Category.Id);
                frage.Variant = stufe;
                var zelle = Zellen(spiel, frage, stufe + 1, 1)[0];
                var ergebnis = new QuestionResult
                {
                    Id = Guid.NewGuid(), GameId = spiel.Game.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = frage.Id, PlayerId = ginaId,
                    Score = 100, RightCount = 1, RecordValue = wert, RecordedAt = DateTimeOffset.UtcNow,
                };
                push.Upserts.AddRange([SyncTestData.New(frage), SyncTestData.New(zelle), SyncTestData.New(ergebnis)]);
            }

            var angelegt = await PushAsync(gina, push);
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            var roh = await gina.GetStringAsync("/api/v1/me/statistics");

            var neu = JsonSerializer.Deserialize<StatisticsResponse>(roh, QuizzerApiClient.Json)!.Leaderboard
                .Categories.Where(c => c.Kind == MinigameKind.MentalArithmetic).ToList();
            CollectionAssert.AreEqual(new int?[] { KopfrechnenAufgaben.Leicht, KopfrechnenAufgaben.Mittel, KopfrechnenAufgaben.Schwer },
                neu.Select(c => c.Variant).ToList());
            CollectionAssert.AreEqual(werte.Select(w => w.Item2).ToList(), neu.Select(c => c.Entries.Single().Value).ToList(),
                "Ein Wert steht in der Liste einer anderen Schwierigkeit.");

            var alt = JsonSerializer.Deserialize<AlteStatistik>(roh, QuizzerApiClient.Json)!.Leaderboard;
            var alteKacheln = alt.Categories
                .Where(c => c.Kind == MinigameKind.MentalArithmetic)
                .Select(c => ($"{c.Title} ({c.KindName})", c.Entries.Single().Value))
                .ToList();

            CollectionAssert.AreEqual(new[]
            {
                ("Schnellster Rechner – leicht (Kopfrechnen)", 1800),
                ("Schnellster Rechner – mittel (Kopfrechnen)", 900),
                ("Schnellster Rechner – schwer (Kopfrechnen)", 300),
            }, alteKacheln, "Ein WPF vom Stand davor zeigt die Schwierigkeiten nicht unterscheidbar.");
        }
    }
}
