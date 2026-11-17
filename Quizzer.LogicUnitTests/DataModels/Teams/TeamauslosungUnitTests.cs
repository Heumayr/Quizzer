using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// „Teams auslosen“ (M37 T2) als reine Rechnung: jeder genau einmal, Größen ±1, das Los mischt Spieler und
    /// Reihenfolge der Teams, unmögliche Zahlen werden abgewiesen statt halb ausgelost (A06).
    /// </summary>
    [TestClass]
    public class TeamauslosungUnitTests
    {
        private static Guid[] Ids(int anzahl) => Enumerable.Range(0, anzahl).Select(_ => Guid.NewGuid()).ToArray();

        /// <summary>Über alle erlaubten Kombinationen bis 20 Spieler: jeder genau einmal, kein Team leer, Größen ±1.</summary>
        [TestMethod]
        public void EveryPlayerLandsOnceAndTheSizesDifferByAtMostOne()
        {
            var geprueft = 0;

            for (var spielerZahl = 2; spielerZahl <= 20; spielerZahl++)
            for (var teamZahl = 2; teamZahl <= Math.Min(TeamGrenzen.MaxTeams, spielerZahl); teamZahl++)
            for (var saat = 0; saat < 4; saat++)
            {
                var spieler = Ids(spielerZahl);
                var teams = Ids(teamZahl);

                var los = Teamauslosung.Lose(spieler, teams, new Random(saat));
                var groessen = teams.Select(t => los.Values.Count(v => v == t)).ToList();
                var fall = $"{spielerZahl} Spieler auf {teamZahl} Teams, Saat {saat}: {string.Join("/", groessen)}";

                CollectionAssert.AreEquivalent(spieler, los.Keys.ToArray(), fall);
                Assert.IsTrue(los.Values.All(teams.Contains), "Ein Spieler landete in einem fremden Team. " + fall);
                Assert.IsTrue(groessen.Max() - groessen.Min() <= 1, "Die Teams sind ungleicher als ±1. " + fall);
                geprueft++;
            }

            Assert.IsTrue(geprueft > 400, $"Nur {geprueft} Fälle - die Schleife greift ins Leere.");
        }

        /// <summary>
        /// Das Los mischt die Spieler: zwei, die in der Eingabe zwei Plätze auseinanderstehen, landen nicht bei jeder
        /// Saat im selben Team. Reihum ohne Mischen wäre es bei zwei Teams immer so.
        /// </summary>
        [TestMethod]
        public void TheDrawMixesThePlayers()
        {
            var spieler = Ids(10);
            var teams = Ids(2);

            var gemeinsam = Enumerable.Range(0, 20)
                .Select(saat => Teamauslosung.Lose(spieler, teams, new Random(saat)))
                .Count(los => los[spieler[0]] == los[spieler[2]]);

            Assert.IsTrue(gemeinsam is > 0 and < 20, $"Spieler 1 und 3 waren in {gemeinsam} von 20 Losen zusammen - das Los mischt nicht.");
        }

        /// <summary>Welches Team bei 5 auf 2 das größere ist, entscheidet das Los - nicht der Platz in der Liste.</summary>
        [TestMethod]
        public void TheLotDecidesWhichTeamIsTheBiggerOne()
        {
            var spieler = Ids(5);
            var teams = Ids(2);

            var erstesGroesser = Enumerable.Range(0, 20)
                .Select(saat => Teamauslosung.Lose(spieler, teams, new Random(saat)))
                .Count(los => los.Values.Count(v => v == teams[0]) == 3);

            Assert.IsTrue(erstesGroesser is > 0 and < 20, $"Das erste Team war in {erstesGroesser} von 20 Losen das größere.");
        }

        /// <summary>Dieselbe Saat, dasselbe Los - die Zufallsquelle kommt von außen.</summary>
        [TestMethod]
        public void TheSameSeedGivesTheSameDraw()
        {
            var spieler = Ids(7);
            var teams = Ids(3);

            CollectionAssert.AreEquivalent(
                Teamauslosung.Lose(spieler, teams, new Random(42)).ToList(),
                Teamauslosung.Lose(spieler, teams, new Random(42)).ToList());
        }

        /// <summary>A06: unmögliche Zahlen werden abgewiesen, mit einem Satz für die Spielleitung.</summary>
        [TestMethod]
        [DataRow(5, 1, DisplayName = "ein Team")]
        [DataRow(10, 9, DisplayName = "mehr Teams als Farben")]
        [DataRow(2, 3, DisplayName = "mehr Teams als Spieler")]
        [DataRow(0, 2, DisplayName = "niemand dabei")]
        public void ImpossibleCountsAreRejected(int spielerZahl, int teamZahl)
        {
            Assert.IsNotNull(Teamauslosung.Problem(spielerZahl, teamZahl));
            Assert.ThrowsExactly<ArgumentException>(() => Teamauslosung.Lose(Ids(spielerZahl), Ids(teamZahl), new Random(1)));
        }

        /// <summary>Die Grenzen selbst sind erlaubt - sonst sagt die Abweisung oben nichts.</summary>
        [TestMethod]
        [DataRow(2, 2)]
        [DataRow(8, 8)]
        [DataRow(20, 8)]
        public void TheLimitsThemselvesAreAllowed(int spielerZahl, int teamZahl)
        {
            Assert.IsNull(Teamauslosung.Problem(spielerZahl, teamZahl));
            Assert.AreEqual(spielerZahl, Teamauslosung.Lose(Ids(spielerZahl), Ids(teamZahl), new Random(1)).Count);
        }

        /// <summary>Ein doppelt gereichter Spieler zählt einmal.</summary>
        [TestMethod]
        public void ADoubledPlayerCountsOnce()
        {
            var spieler = Ids(3);

            var los = Teamauslosung.Lose(spieler.Append(spieler[0]), Ids(2), new Random(3));

            Assert.AreEqual(3, los.Count);
        }
    }
}
