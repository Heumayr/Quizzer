using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Spielablauf;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// P8-5 (Durchsicht M37, P10): eine Zeile mit „richtig beantwortet“, aber ohne Punkte wird gespeichert - über das Häkchen
    /// „Richtig beantwortet“ auf der Spielerkarte und „Speichern“. An ihr gehen die beiden Fragen „gebucht?“ auseinander, mit
    /// Absicht: die gespeicherte Zeile trägt ein Ergebnis (wer auswählt, Gewinner der Zelle), gebucht ist aber nichts.
    /// </summary>
    [TestClass]
    public class RichtigOhnePunkteUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        [TestMethod]
        public async Task TheTickWithoutPointsIsStoredAsAResultButNotAsABooking()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            var ergebnisse = vm.PlayersResultViewModel!;
            var karte = ergebnisse.PlayerResultContextList[0];
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate, ergebnisse.Results.Select(r => r.CloneWithoutReferences()).ToList());

            karte.CorrectAnswered = true; // das Häkchen auf der Karte, ohne Bewertung
            Assert.IsFalse(ergebnisse.HatOffeneBewertungen, "Das Häkchen gilt als Bewertung - dann ist es nicht der gemessene Weg.");
            Assert.IsTrue(await ergebnisse.TrySaveAsync(), "Speichern scheiterte.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            using var ctrl = new QuestionResultsController();
            var zeile = (await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id))!.Single(r => r.PlayerId == karte.Player.Id);

            Assert.IsTrue(zeile.CorrectAnswered, "Das Häkchen wurde nicht gespeichert.");
            Assert.AreEqual((0, 0, 0, 0), (zeile.RightCount, zeile.WrongCount, zeile.CorrectionsCount, zeile.FinalScore),
                "Mit dem Häkchen wurden Punkte oder Zähler gebucht.");

            Assert.IsFalse(Teamwertung.OhneBuchung(zeile), "Die Zeile mit „richtig beantwortet“ gilt als leer - sie verlöre Stempel und Platz.");
            Assert.IsFalse(merk.HatGebucht([zeile]), "Das Häkchen ohne Punkte gilt als Buchung - es sperrte Wiederholen und Joker.");
        }
    }
}
