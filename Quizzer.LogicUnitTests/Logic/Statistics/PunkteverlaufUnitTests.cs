using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Statistics;

namespace Quizzer.LogicUnitTests.Logic.Statistics
{
    /// <summary>
    /// Der Punkteverlauf (M37 E06) als reine Rechnung: Reihenfolge nach dem Zeitpunkt, nicht nach dem Raster; Stand je
    /// Spieler aufsummiert; ohne Zeitpunkt an einer gespielten Zelle (altes Spiel) kein Verlauf.
    /// </summary>
    [TestClass]
    public class PunkteverlaufUnitTests
    {
        private static readonly DateTimeOffset Abend = new(2026, 11, 16, 20, 0, 0, TimeSpan.FromHours(1));

        private readonly Player anna = new() { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna" };
        private readonly Player bert = new() { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert" };

        private static GameGridCoordinate Zelle(int x, int? minuten, bool gespielt = true)
            => new()
            {
                Id = Guid.NewGuid(), X = x, Y = 0, QuestionBaseId = Guid.NewGuid(), IsDone = gespielt,
                PlayedAt = minuten is { } m ? Abend.AddMinutes(m) : null,
            };

        private static QuestionResult Ergebnis(GameGridCoordinate zelle, Player spieler, int plus, int minus = 0)
            => new() { Id = Guid.NewGuid(), GameGridCoordinateId = zelle.Id, PlayerId = spieler.Id, Score = plus, MinusScore = minus };

        [TestMethod]
        public void TheLinesFollowThePlayedTimeNotTheGrid()
        {
            // Rasterlinks (x 0) wurde zuletzt gespielt.
            var spaet = Zelle(0, minuten: 40);
            var frueh = Zelle(1, minuten: 10);
            var mitte = Zelle(2, minuten: 25);

            var verlauf = Punkteverlauf.Rechne([spaet, frueh, mitte],
                [Ergebnis(frueh, anna, 100), Ergebnis(mitte, bert, 200), Ergebnis(mitte, anna, 0, minus: 50), Ergebnis(spaet, bert, 300)],
                [anna, bert]);

            Assert.IsNotNull(verlauf);
            CollectionAssert.AreEqual(new[] { frueh.Id, mitte.Id, spaet.Id }, verlauf.Zellen.ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 200, 500 }, verlauf.Reihen.Single(r => r.PlayerId == bert.Id).Punkte.ToArray());
            CollectionAssert.AreEqual(new[] { 0, 100, 50, 50 }, verlauf.Reihen.Single(r => r.PlayerId == anna.Id).Punkte.ToArray());
            Assert.AreEqual(bert.Id, verlauf.Reihen[0].PlayerId, "Die Reihen stehen nach Endstand - der Fuehrende zuerst.");
        }

        /// <summary>Eine offene Zelle (nur geöffnet, Ergebniszeilen schon angelegt) gehört nicht in den Verlauf.</summary>
        [TestMethod]
        public void AnOpenCellAndItsResultsStayOut()
        {
            var gespielt = Zelle(0, minuten: 5);
            var offen = Zelle(1, minuten: null, gespielt: false);

            var verlauf = Punkteverlauf.Rechne([gespielt, offen], [Ergebnis(gespielt, anna, 100), Ergebnis(offen, anna, 999)], [anna]);

            Assert.IsNotNull(verlauf, "Eine offene Zelle ohne Zeitpunkt verhindert den Verlauf - sie ist ja nicht gespielt.");
            CollectionAssert.AreEqual(new[] { 0, 100 }, verlauf.Reihen.Single().Punkte.ToArray());
        }

        /// <summary>Ein altes Spiel: eine gespielte Zelle ohne Zeitpunkt - keine erfundene Reihenfolge, kein Diagramm.</summary>
        [TestMethod]
        public void APlayedCellWithoutTimeMeansNoChart()
        {
            var neu = Zelle(0, minuten: 5);
            var alt = Zelle(1, minuten: null);

            Assert.IsNull(Punkteverlauf.Rechne([neu, alt], [Ergebnis(neu, anna, 100)], [anna]));
        }

        [TestMethod]
        public void WithoutAPlayedCellThereIsNoChart()
            => Assert.IsNull(Punkteverlauf.Rechne([Zelle(0, null, gespielt: false)], [], [anna]));

        /// <summary>Eine leere Rasterstelle (ohne Frage) zählt nie als gespielt - auch nicht mit IsDone.</summary>
        [TestMethod]
        public void AnEmptyGridCellIsIgnored()
        {
            var leer = new GameGridCoordinate { Id = Guid.NewGuid(), IsDone = true };
            var gespielt = Zelle(0, minuten: 5);

            var verlauf = Punkteverlauf.Rechne([leer, gespielt], [Ergebnis(gespielt, bert, 100)], [anna, bert]);

            Assert.IsNotNull(verlauf);
            Assert.AreEqual(1, verlauf.Zellen.Count);
            CollectionAssert.AreEqual(new[] { 0, 0 }, verlauf.Reihen.Single(r => r.PlayerId == anna.Id).Punkte.ToArray(),
                "Wer nichts gewinnt, bekommt trotzdem eine Linie - auf null.");
        }

        [TestMethod]
        public void TheSameTimeOrdersByGridPosition()
        {
            var rechts = Zelle(2, minuten: 5);
            var links = Zelle(1, minuten: 5);

            var verlauf = Punkteverlauf.Rechne([rechts, links], [], [anna]);

            CollectionAssert.AreEqual(new[] { links.Id, rechts.Id }, verlauf!.Zellen.ToArray());
        }
    }
}
