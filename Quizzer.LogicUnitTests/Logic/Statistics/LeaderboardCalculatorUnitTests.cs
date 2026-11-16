using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Statistics;

namespace Quizzer.LogicUnitTests.Logic.Statistics
{
    /// <summary>
    /// Bestenliste ueber Abende (M35 C9) als reine Rechnung: Richtung je Art, Gleichstand, hoechstens fuenf, Klaus ueber
    /// Spiele, und nur Arten mit Bestwert.
    /// </summary>
    [TestClass]
    public class LeaderboardCalculatorUnitTests
    {
        private static readonly Guid SpielA = Guid.NewGuid();
        private static readonly Guid SpielB = Guid.NewGuid();
        private static readonly DateTimeOffset Abend = new(2026, 11, 16, 20, 0, 0, TimeSpan.FromHours(1));

        private static readonly Dictionary<string, Guid> Ids = new();

        private static Guid Id(string name)
        {
            if (!Ids.TryGetValue(name, out var id))
                Ids[name] = id = Guid.NewGuid();
            return id;
        }

        private static LeaderboardRow Zeile(string spieler, MinigameKind? art, int? wert, int klaus = 0, Guid? spiel = null, int minute = 0)
            => new(Guid.NewGuid(), Id(spieler), spieler, spiel ?? SpielA, spiel == SpielB ? "Abend B" : "Abend A",
                art, wert, wert == null ? null : Abend.AddMinutes(minute), klaus);

        private static LeaderboardCategory Liste(Leaderboard board, MinigameKind art) => board.Categories.Single(c => c.Kind == art);

        private static List<string> Namen(LeaderboardCategory liste) => liste.Entries.Select(e => e.PlayerName).ToList();

        [TestMethod]
        public void LowerIsBetterSortsUpwardsAndHigherIsBetterDownwards()
        {
            var board = LeaderboardCalculator.Calculate(
            [
                Zeile("Anna", MinigameKind.Reaction, 300), Zeile("Bert", MinigameKind.Reaction, 187), Zeile("Carl", MinigameKind.Reaction, 250),
                Zeile("Anna", MinigameKind.TapSprint, 500), Zeile("Bert", MinigameKind.TapSprint, 730), Zeile("Carl", MinigameKind.TapSprint, 610),
            ]);

            CollectionAssert.AreEqual(new[] { "Bert", "Carl", "Anna" }, Namen(Liste(board, MinigameKind.Reaction)), "Die kuerzeste Zeit steht nicht oben.");
            CollectionAssert.AreEqual(new[] { "Bert", "Carl", "Anna" }, Namen(Liste(board, MinigameKind.TapSprint)), "Die meisten Tipps stehen nicht oben.");
            Assert.AreEqual("187 ms", Liste(board, MinigameKind.Reaction).Entries[0].ValueText);
            Assert.AreEqual("Abend A", Liste(board, MinigameKind.Reaction).Entries[0].GameName);
            Assert.AreEqual(Abend, Liste(board, MinigameKind.Reaction).Entries[0].RecordedAt);
        }

        [TestMethod]
        public void ATieSharesTheRankAndWhoHadItFirstStandsFirst_WhateverTheInputOrder()
        {
            // Zora war frueher - der Name allein saehe Anna vorn.
            var zeilen = new[]
            {
                Zeile("Anna", MinigameKind.Reaction, 200, minute: 30),
                Zeile("Zora", MinigameKind.Reaction, 200, minute: 10),
                Zeile("Bert", MinigameKind.Reaction, 150, minute: 50),
                Zeile("Carl", MinigameKind.Reaction, 400, minute: 0),
            };

            var vorwaerts = Liste(LeaderboardCalculator.Calculate(zeilen), MinigameKind.Reaction);
            var rueckwaerts = Liste(LeaderboardCalculator.Calculate(zeilen.Reverse()), MinigameKind.Reaction);

            CollectionAssert.AreEqual(new[] { "Bert", "Zora", "Anna", "Carl" }, Namen(vorwaerts));
            CollectionAssert.AreEqual(Namen(vorwaerts), Namen(rueckwaerts), "Die Reihenfolge haengt an der Eingabe.");
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 4 }, vorwaerts.Entries.Select(e => e.Rank).ToList(), "Gleichstand teilt den Platz nicht.");
        }

        [TestMethod]
        public void AtMostFiveEntriesAndEachPlayerOnlyWithHisBest()
        {
            var zeilen = Enumerable.Range(1, 7).Select(i => Zeile("Spieler " + i, MinigameKind.Reaction, 200 + i)).ToList();
            zeilen.Add(Zeile("Spieler 1", MinigameKind.Reaction, 150, spiel: SpielB));
            zeilen.Add(Zeile("Spieler 1", MinigameKind.Reaction, 160));

            var liste = Liste(LeaderboardCalculator.Calculate(zeilen), MinigameKind.Reaction);

            Assert.AreEqual(LeaderboardCalculator.Top, liste.Entries.Count, "Nicht hoechstens fuenf.");
            Assert.AreEqual(1, liste.Entries.Count(e => e.PlayerName == "Spieler 1"), "Ein Spieler steht mehrfach.");
            Assert.AreEqual((150, "Abend B"), (liste.Entries[0].Value, liste.Entries[0].GameName), "Nicht sein bester Wert.");
        }

        [TestMethod]
        public void StealsAreSummedOverGamesAndNobodyWithoutStands()
        {
            var board = LeaderboardCalculator.Calculate(
            [
                Zeile("Anna", null, null, klaus: 1, spiel: SpielA),
                Zeile("Anna", MinigameKind.RobberAndGuard, null, klaus: 2, spiel: SpielB),
                Zeile("Bert", null, null, klaus: 2, spiel: SpielA),
                Zeile("Carl", MinigameKind.Reaction, 250, klaus: 0),
            ]);

            CollectionAssert.AreEqual(new[] { ("Anna", 3, 1), ("Bert", 2, 2) },
                board.MostSteals.Select(s => (s.PlayerName, s.Steals, s.Rank)).ToList(), "Die Klaus sind nicht ueber die Spiele summiert.");
        }

        [TestMethod]
        public void KindsWithoutARecordAndImplausibleValuesDoNotAppear()
        {
            var reaktion = MinigameProfiles.For(MinigameKind.Reaction).Record ?? throw new AssertFailedException("Reaktion ohne Bestwert.");
            var board = LeaderboardCalculator.Calculate(
            [
                Zeile("Anna", MinigameKind.Majority, 250),
                Zeile("Bert", null, 250),
                Zeile("Carl", MinigameKind.Reaction, reaktion.Min - 1),
            ]);

            var mitBestwert = MinigameProfiles.All.Where(p => p.Record != null).Select(p => p.Kind).ToList();

            CollectionAssert.AreEqual(mitBestwert, board.Categories.Select(c => c.Kind).ToList(), "Eine Art ohne Bestwert hat eine Liste.");
            Assert.IsTrue(board.Categories.All(c => c.Entries.Count == 0), "Ein Wert ohne Bestwert-Art oder ausserhalb des Bereichs steht in einer Liste.");
        }
    }
}
