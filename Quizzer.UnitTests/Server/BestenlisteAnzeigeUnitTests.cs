using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Views.Bestenliste;

namespace Quizzer.UnitTests.Server
{
    /// <summary>Die Kacheln der Bestenliste in „Meine Statistik" (M35 C9) und ohne Server (M37 E03): je Art eine Kachel, mit Spiel und Datum, leer mit Satz.</summary>
    [TestClass]
    public class BestenlisteAnzeigeUnitTests
    {
        private static readonly DateTimeOffset Am = new(2026, 11, 16, 21, 0, 0, TimeSpan.FromHours(1));

        [TestMethod]
        public void EachEntryNamesPlayerValueGameAndDate()
        {
            var eintrag = new LeaderboardEntry(1, Guid.NewGuid(), "Anna", 187, "187 ms", Guid.NewGuid(), "Spieleabend", Am);

            Assert.AreEqual("1. Anna – 187 ms (Spieleabend, 16.11.2026)", BestenlisteKacheln.Zeile(eintrag));
            Assert.AreEqual("1. Anna – 187 ms (Spieleabend)", BestenlisteKacheln.Zeile(eintrag with { RecordedAt = null }),
                "Ohne Datum steht ein leeres Komma da.");
        }

        [TestMethod]
        public void EveryListHasATileAndAnEmptyOneSaysSo()
        {
            var liste = new Leaderboard(
                [
                    new LeaderboardCategory(MinigameKind.Reaction, "Schnellste Reaktion", "Reaktion", "ms", true,
                        [new LeaderboardEntry(1, Guid.NewGuid(), "Anna", 187, "187 ms", Guid.NewGuid(), "Spieleabend", Am)]),
                    new LeaderboardCategory(MinigameKind.Stopwatch, "Genaueste Stoppuhr", "Stoppuhr", "ms daneben", true, []),
                ],
                [new StealsEntry(1, Guid.NewGuid(), "Bert", 3), new StealsEntry(2, Guid.NewGuid(), "Carl", 1)]);

            var kacheln = BestenlisteKacheln.Aus(liste).ToList();

            CollectionAssert.AreEqual(new[] { "Schnellste Reaktion (Reaktion)", "Genaueste Stoppuhr (Stoppuhr)", "Meiste Klaus" },
                kacheln.Select(k => k.Titel).ToList());
            CollectionAssert.AreEqual(new[] { "Noch kein Eintrag." }, kacheln[1].Zeilen.ToList(), "Eine leere Liste sagt es nicht.");
            CollectionAssert.AreEqual(new[] { "1. Bert – 3 Klaus", "2. Carl – 1 Klau" }, kacheln[2].Zeilen.ToList());
        }
    }
}
