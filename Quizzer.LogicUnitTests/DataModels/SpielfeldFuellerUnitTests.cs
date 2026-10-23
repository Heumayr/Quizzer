using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>M32: leere Zellen mit passenden Fragen füllen - Kategorie je Spalte, leicht oben, schwer unten.</summary>
    [TestClass]
    public class SpielfeldFuellerUnitTests
    {
        private static readonly Category Erdkunde = new() { Id = Guid.NewGuid(), Designation = "Erdkunde" };
        private static readonly Category Musik = new() { Id = Guid.NewGuid(), Designation = "Musik" };

        private static Game Feld(int breite, int hoehe, params string[] koepfe)
        {
            var spiel = new Game { Id = Guid.NewGuid(), Width = breite, Height = hoehe };

            for (var x = 0; x < breite; x++)
                for (var y = 0; y < hoehe; y++)
                    spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), X = x, Y = y, GameId = spiel.Id });

            for (var i = 0; i < koepfe.Length; i++)
                spiel.Headers.Add(new Header { Id = Guid.NewGuid(), HeaderType = HeaderType.Column, Index = i, Designation = koepfe[i] });

            return spiel;
        }

        private static QuestionBase Frage(Category k, int punkte, bool ungeprueft = false)
        {
            var q = Factory.CreateNewQuestion(QuestionType.Default);
            q.Id = Guid.NewGuid();
            q.CategoryId = k.Id;
            q.Points = punkte;
            q.Designation = $"{k.Designation} {punkte}";
            q.KiUngeprueft = ungeprueft;
            return q;
        }

        [TestMethod]
        public void AColumnTakesItsCategoryFromTheHeaderAndCheapQuestionsGoOnTop()
        {
            var spiel = Feld(1, 3, "erdkunde");
            var fragen = new[] { Frage(Erdkunde, 300), Frage(Erdkunde, 100), Frage(Erdkunde, 200), Frage(Musik, 100) };

            var ergebnis = SpielfeldFueller.Vorschlag(spiel, fragen, [Erdkunde, Musik]);

            CollectionAssert.AreEqual(new[] { 100, 200, 300 },
                ergebnis.Belegungen.OrderBy(b => b.Zelle.Y).Select(b => b.Frage.Points).ToArray());
            Assert.IsTrue(ergebnis.Belegungen.All(b => b.Frage.CategoryId == Erdkunde.Id), "Eine fremde Kategorie kam in die Spalte.");
            Assert.AreEqual(0, ergebnis.Hinweise.Count);
        }

        [TestMethod]
        public void UsedQuestionsAndUncheckedDraftsAreNeverProposed()
        {
            var spiel = Feld(1, 3, "Musik");
            var liegt = Frage(Musik, 100);
            spiel.GameGridCoordinates[0].QuestionBaseId = liegt.Id;
            spiel.GameGridCoordinates[0].QuestionBase = liegt;

            var fragen = new[] { liegt, Frage(Musik, 200), Frage(Musik, 300, ungeprueft: true) };
            var ergebnis = SpielfeldFueller.Vorschlag(spiel, fragen, [Musik]);

            Assert.AreEqual(1, ergebnis.Belegungen.Count);
            Assert.AreEqual(200, ergebnis.Belegungen[0].Frage.Points);
            Assert.IsTrue(ergebnis.Hinweise.Single().Contains("1 von 2"), ergebnis.Hinweise.Single());
        }

        [TestMethod]
        public void WithoutAMatchingHeaderTheColumnFollowsWhatAlreadyLiesThere()
        {
            var spiel = Feld(2, 2, "", "Unbekannt");
            var liegt = Frage(Musik, 100);
            spiel.GameGridCoordinates[0].QuestionBaseId = liegt.Id;
            spiel.GameGridCoordinates[0].QuestionBase = liegt;

            var ergebnis = SpielfeldFueller.Vorschlag(spiel, [liegt, Frage(Musik, 400), Frage(Erdkunde, 100)], [Musik, Erdkunde]);

            Assert.AreEqual(1, ergebnis.Belegungen.Count, "Nur Spalte 1 hat eine Kategorie.");
            Assert.AreEqual(Musik.Id, ergebnis.Belegungen[0].Frage.CategoryId);
            Assert.IsTrue(ergebnis.Hinweise.Any(h => h.Contains("„Unbekannt“")), string.Join("; ", ergebnis.Hinweise));
        }

        [TestMethod]
        public void NoQuestionLandsTwiceEvenWhenTwoColumnsShareACategory()
        {
            var spiel = Feld(2, 2, "Erdkunde", "Erdkunde");
            var ergebnis = SpielfeldFueller.Vorschlag(spiel, [Frage(Erdkunde, 100), Frage(Erdkunde, 200), Frage(Erdkunde, 300)], [Erdkunde]);

            Assert.AreEqual(3, ergebnis.Belegungen.Count);
            Assert.AreEqual(3, ergebnis.Belegungen.Select(b => b.Frage.Id).Distinct().Count());
        }
    }
}
