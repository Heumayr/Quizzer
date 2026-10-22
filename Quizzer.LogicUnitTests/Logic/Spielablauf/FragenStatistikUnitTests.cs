using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Spielablauf
{
    /// <summary>V5: Statistik je Frage aus den abgeschlossenen Zellen.</summary>
    [TestClass]
    public class FragenStatistikUnitTests
    {
        [TestMethod]
        public void TheRecordReadsWell()
        {
            Assert.AreEqual("–", FragenStatistik.Nie.QuoteText);
            Assert.AreEqual("nie", FragenStatistik.Nie.GespieltText);
            Assert.AreEqual(-1, FragenStatistik.Nie.Sortierwert);

            var s = new FragenStatistik(Gespielt: 2, Antworten: 6, Richtig: 4, Punkte: 700);
            Assert.AreEqual("67 % (4/6)", s.QuoteText);
            Assert.AreEqual("2-mal", s.GespieltText);
            Assert.AreEqual(string.Empty, s.Einschaetzung);

            Assert.AreEqual("leicht", new FragenStatistik(1, 5, 4, 0).Einschaetzung);
            Assert.AreEqual("schwer", new FragenStatistik(1, 5, 1, 0).Einschaetzung);
            Assert.AreEqual(string.Empty, new FragenStatistik(1, 2, 0, 0).Einschaetzung, "Zwei Antworten sind kein Urteil.");
        }

        [TestMethod]
        public async Task OnlyFinishedCellsCountAndEachQuestionStandsAlone()
        {
            TestDatabase.ClearDiscardedChanges();

            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Statistik" };
            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            QuestionBase Frage(string name) => new DefaultQuestion { Id = Guid.NewGuid(), Designation = name, DesignationShort = name[..1], CategoryId = kategorie.Id, Points = 100 };
            var leicht = Frage("Leicht");
            var offen = Frage("Offen");
            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(leicht);
                await ctrl.UpsertAsync(offen);
                await ctrl.SaveChangesAsync();
            }

            var spieler = new[] { "A", "B", "C" }.Select(n => new Player { Id = Guid.NewGuid(), Designation = "Stat" + n }).ToList();
            using (var ctrl = new PlayersController())
            {
                foreach (var p in spieler)
                    await ctrl.InsertAsync(p);
                await ctrl.SaveChangesAsync();
            }

            // Zwei Spiele: die leichte Frage beide Male abgeschlossen, die offene nur geöffnet.
            foreach (var runde in new[] { 1, 2 })
            {
                var spiel = new Game { Id = Guid.NewGuid(), Designation = "Stat " + runde, Phase = 1, SuggestedPhases = 1 };
                using (var ctrl = new GamesController())
                {
                    await ctrl.UpsertAsync(spiel);
                    await ctrl.SaveChangesAsync();
                }

                var zelleLeicht = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, Phase = 1, QuestionBaseId = leicht.Id, IsDone = true };
                var zelleOffen = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, X = 1, Y = 0, Phase = 1, QuestionBaseId = offen.Id, IsDone = false };

                using var ctrlZellen = new GameGridCoordinatesController();
                using var ctrlErgebnis = new QuestionResultsController(ctrlZellen);
                await ctrlZellen.InsertAsync(zelleLeicht);
                await ctrlZellen.InsertAsync(zelleOffen);

                for (var i = 0; i < spieler.Count; i++)
                {
                    // Runde 1: A und B richtig; Runde 2: nur A.
                    var richtig = i == 0 || (i == 1 && runde == 1);
                    await ctrlErgebnis.InsertAsync(new QuestionResult
                    {
                        Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler[i].Id, GameGridCoordinateId = zelleLeicht.Id, QuestionBaseId = leicht.Id,
                        CorrectAnswered = richtig, Score = richtig ? 100 : 0, MinusScore = richtig ? 0 : 50,
                    });
                    await ctrlErgebnis.InsertAsync(new QuestionResult
                    {
                        Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler[i].Id, GameGridCoordinateId = zelleOffen.Id, QuestionBaseId = offen.Id,
                    });
                }

                await ctrlZellen.SaveChangesAsync();
            }

            Dictionary<Guid, FragenStatistik> statistik;
            using (var ctrl = new QuestionResultsController())
                statistik = await ctrl.StatistikJeFrageAsync();

            Assert.AreEqual(new FragenStatistik(Gespielt: 2, Antworten: 6, Richtig: 3, Punkte: 3 * 100 - 3 * 50), statistik[leicht.Id]);
            Assert.IsFalse(statistik.ContainsKey(offen.Id), "Eine nur geöffnete Frage zählt mit - lauter falsch.");
        }
    }
}
