using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Teams
{
    /// <summary>
    /// Die Teamsumme (M37 T3): <see cref="Game.Teamstand"/> über den Stempel, nicht über die heutige Zuordnung (F-M37-07);
    /// ohne Stempel beim aktuellen Team, ohne Team bei keinem; ein fremder Stempel zählt nirgends (A08). Die Rangfolge
    /// teilt bei Gleichstand den Platz.
    /// </summary>
    [TestClass]
    public class TeamwertungUnitTests
    {
        private Game spiel = null!;
        private GameGridCoordinate zelle = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;

        [TestInitialize]
        public void SetUp()
        {
            spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id };
            spiel.GameGridCoordinates.Add(zelle);
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            spiel.GameTeams.AddRange([rot, blau]);
        }

        private PlayerXGame Setze(string name, GameTeam? team)
        {
            var spieler = new Player { Id = Guid.NewGuid(), DisplayName = name };
            var platz = new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = team?.Id };
            spiel.PlayerXGames.Add(platz);
            return platz;
        }

        /// <summary>Ein Ergebnis an der Zelle; <paramref name="stempel"/> ist das Team beim Öffnen (T2), <c>null</c> = ohne.</summary>
        private QuestionResult Buche(PlayerXGame platz, int punkte, Guid? stempel, int minus = 0)
        {
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                GameGridCoordinateId = zelle.Id,
                PlayerId = platz.PlayerId,
                Score = punkte,
                MinusScore = minus,
                GameTeamId = stempel,
            };
            zelle.QuestionResults.Add(ergebnis);
            return ergebnis;
        }

        [TestMethod]
        public void TheTeamSumAddsTheStampedResultsOfItsMembers()
        {
            var anna = Setze("Anna", rot);
            var bert = Setze("Bert", rot);
            var carl = Setze("Carl", blau);
            Buche(anna, 300, rot.Id);
            Buche(bert, 200, rot.Id, minus: 50);
            Buche(carl, 100, blau.Id);

            Assert.AreEqual(450, spiel.Teamstand(rot.Id), "Rot: 300 + 200 − 50.");
            Assert.AreEqual(100, spiel.Teamstand(blau.Id));
            Assert.AreEqual(spiel.Kontostand(anna.PlayerId) + spiel.Kontostand(bert.PlayerId), spiel.Teamstand(rot.Id),
                "Ohne Umsetzen ist die Teamsumme die Summe der Kontostände - die Punkte bleiben am Spieler (F-M37-01).");
        }

        /// <summary>
        /// F-M37-07: wer umgesetzt wird, lässt seine Punkte beim alten Team. Der Verlauf wird gemessen - erst buchen, dann
        /// umsetzen, dann neu buchen -, nicht nur der Endstand.
        /// </summary>
        [TestMethod]
        public void MovingAPlayerLeavesHisPointsWithTheOldTeam()
        {
            var anna = Setze("Anna", rot);
            Buche(anna, 300, rot.Id);
            Assert.AreEqual(300, spiel.Teamstand(rot.Id), "Vor dem Umsetzen zählt Annas Ergebnis nicht bei Rot - dann misst der Test nichts.");

            anna.GameTeamId = blau.Id;
            Buche(anna, 100, blau.Id);

            Assert.AreEqual(300, spiel.Teamstand(rot.Id), "Das Umsetzen hat Annas alte Punkte von Rot abgezogen.");
            Assert.AreEqual(100, spiel.Teamstand(blau.Id), "Blau zählt Annas Punkte aus der Zeit bei Rot mit.");
            Assert.AreEqual(400, spiel.Kontostand(anna.PlayerId), "Am Spieler bleiben alle Punkte.");
        }

        /// <summary>
        /// Ohne Stempel (Beitritt am Telefon, M10: Ergebnisse vor dem Zuteilen) zählt ein Ergebnis beim aktuellen Team - und
        /// wandert mit, solange es keinen Stempel hat. Ohne Team zählt es bei keinem.
        /// </summary>
        [TestMethod]
        public void WithoutStampTheResultCountsWithTheCurrentTeamOrWithNone()
        {
            var dora = Setze("Dora", null);
            Buche(dora, 200, stempel: null);

            Assert.AreEqual(0, spiel.Teamstand(rot.Id) + spiel.Teamstand(blau.Id), "Ein Ergebnis ohne Stempel und ohne Team zählt bei einem Team.");
            Assert.IsNull(Teamwertung.TeamDes(spiel, zelle.QuestionResults.Single()));

            dora.GameTeamId = blau.Id;
            Assert.AreEqual(200, spiel.Teamstand(blau.Id), "Nach dem Zuteilen zählt das Ergebnis ohne Stempel nicht beim aktuellen Team.");

            dora.GameTeamId = rot.Id;
            Assert.AreEqual(200, spiel.Teamstand(rot.Id), "Ohne Stempel wandert das Ergebnis nicht mit dem Spieler.");
            Assert.AreEqual(0, spiel.Teamstand(blau.Id));
        }

        /// <summary>A08: ein Stempel, der kein Team dieses Spiels ist, zählt nirgends - nicht ersatzweise beim aktuellen Team.</summary>
        [TestMethod]
        public void AForeignStampCountsForNoTeamOfThisGame()
        {
            var anna = Setze("Anna", rot);
            Buche(anna, 500, stempel: Guid.NewGuid());

            Assert.AreEqual(0, spiel.Teamstand(rot.Id), "Ein fremder Stempel zählt ersatzweise beim aktuellen Team.");
            Assert.AreEqual(0, spiel.Teamstand(blau.Id));
            Assert.AreEqual(500, spiel.Kontostand(anna.PlayerId), "Am Spieler bleiben die Punkte trotzdem.");
        }

        /// <summary>Gleichstand teilt den Platz (1, 1, 3); ein leeres Team steht mit 0 in der Rangfolge.</summary>
        [TestMethod]
        public void ATieSharesThePlace()
        {
            var gelb = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Gelb", ColorIndex = 3, SortOrder = 2 };
            var gruen = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Grün", ColorIndex = 2, SortOrder = 3 };
            spiel.GameTeams.AddRange([gelb, gruen]);
            Buche(Setze("Anna", blau), 300, blau.Id);
            Buche(Setze("Bert", rot), 300, rot.Id);
            Buche(Setze("Carl", gelb), 100, gelb.Id);

            var rangfolge = Teamwertung.Rangfolge(spiel);

            CollectionAssert.AreEqual(new[] { "Rot", "Blau", "Gelb", "Grün" }, rangfolge.Select(p => p.Team.Name).ToArray(),
                "Gleichauf in der Reihenfolge des Spielaufbaus, dann nach Summe.");
            CollectionAssert.AreEqual(new[] { 1, 1, 3, 4 }, rangfolge.Select(p => p.Platz).ToArray(), "Gleichstand teilt den Platz nicht.");
            CollectionAssert.AreEqual(new[] { 300, 300, 100, 0 }, rangfolge.Select(p => p.Summe).ToArray());
        }

        /// <summary>R3-1: Rot −200, Blau −100, Gelb leer - Gelb stand mit 0 auf Platz 1 und wurde Siegerteam.</summary>
        [TestMethod]
        public void AnEmptyTeamIsNotRankedAndCannotWin()
        {
            var gelb = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Gelb", ColorIndex = 3, SortOrder = 2 };
            spiel.GameTeams.Add(gelb);
            Buche(Setze("Anna", rot), 0, rot.Id, minus: 200);
            Buche(Setze("Bert", blau), 0, blau.Id, minus: 100);

            var rangfolge = Teamwertung.Rangfolge(spiel);

            CollectionAssert.AreEqual(new[] { "Blau", "Rot" }, rangfolge.Select(p => p.Team.Name).ToArray(), "Das leere Team steht in der Rangfolge.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, rangfolge.Select(p => p.Platz).ToArray());
        }

        /// <summary>R3-1: ein Team ohne Mitglieder, aber mit Ergebnissen unter seinem Stempel, spielt mit - die Punkte bleiben (F-M37-07).</summary>
        [TestMethod]
        public void ATeamWithStampedResultsStaysRankedAfterItsMembersMoved()
        {
            var anna = Setze("Anna", rot);
            Buche(anna, 300, rot.Id);
            Buche(Setze("Bert", blau), 100, blau.Id);
            anna.GameTeamId = blau.Id;

            var rangfolge = Teamwertung.Rangfolge(spiel);

            CollectionAssert.AreEqual(new[] { ("Rot", 1, 300), ("Blau", 2, 100) }, rangfolge.Select(p => (p.Team.Name, p.Platz, p.Summe)).ToArray(),
                "Rot verliert mit dem letzten Mitglied seinen Platz, obwohl es Punkte hat.");
        }

        /// <summary>Ohne Teamspiel gibt es keine Rangfolge - auch wenn Teams und Stempel noch dastehen.</summary>
        [TestMethod]
        public void WithoutTeamGameThereIsNoRanking()
        {
            Buche(Setze("Anna", rot), 300, rot.Id);
            spiel.IsTeamGame = false;

            Assert.AreEqual(0, Teamwertung.Rangfolge(spiel).Count);
        }

        /// <summary>
        /// A08, Grundsatz des Entwurfs: die Teamsumme wird nie gespeichert. Hält, dass niemand dem Team eine Punktespalte
        /// gibt - eine gespeicherte Summe liefe dem Stempel davon und wäre über den Abgleich fälschbar.
        /// </summary>
        [TestMethod]
        public void TheTeamHasNoStoredScore()
        {
            var verdaechtig = typeof(GameTeam).GetProperties()
                .Select(p => p.Name)
                .Where(n => new[] { "Score", "Punkte", "Summe", "Stand", "Points" }.Any(w => n.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.AreEqual(0, verdaechtig.Count, "Das Team trägt eine Punktespalte: " + string.Join(", ", verdaechtig));
        }
    }
}
