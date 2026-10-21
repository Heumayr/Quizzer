using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using static LocalBuzzer.Service.Base.States.BuzzerKeySelector;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>Wahr oder falsch (M12): Maske, Wertung je Aussage, Prüfung der Abgabe am Buzzer-Server.</summary>
    [TestClass]
    public class WahrFalschUnitTests
    {
        private static readonly (string Text, bool Wahr)[] Aussagen =
            [("Wale sind Säugetiere.", true), ("Spinnen sind Insekten.", false), ("Bambus ist ein Gras.", true)];

        private static TrueFalseQuestion FrageAusDerMaske()
        {
            var frage = (TrueFalseQuestion)Factory.CreateNewQuestion(QuestionType.TrueFalse);
            frage.Id = Guid.NewGuid();
            frage.Designation = "Natur";
            frage.DesignationShort = "N-1";
            frage.CategoryId = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.TrueFalse);
            var bild = composer.Lies(frage);

            for (var i = 0; i < Aussagen.Length; i++)
            {
                bild.Zeilen[i].Text = Aussagen[i].Text;
                bild.Zeilen[i].IstRichtig = Aussagen[i].Wahr;
            }

            composer.Schreib(frage, bild);
            frage.CalculateOrderdSteps();

            return frage;
        }

        private static List<string> TastenDerWahren(QuestionBase frage)
            => WahrFalschEvaluator.Aussagen(frage).Where(s => s.IsResult).Select(s => s.QuestionViewKey).ToList();

        [TestMethod]
        public void TheMaskAllowsEveryStatementAndPassesTheValidator()
        {
            var frage = FrageAusDerMaske();

            Assert.AreEqual(3, frage.BuzzerMaxAllowedKeySelect, "Jede Aussage muss als wahr waehlbar sein.");
            Assert.AreEqual(0, QuestionValidator.Validate(frage).Count,
                string.Join("; ", QuestionValidator.Validate(frage).Select(i => i.Message)));
        }

        [TestMethod]
        public void AllRightGetsFullPoints()
        {
            var frage = FrageAusDerMaske();

            var ergebnis = WahrFalschEvaluator.Werte(frage, TastenDerWahren(frage), 90);

            Assert.IsTrue(ergebnis.AllesRichtig);
            Assert.AreEqual(90, ergebnis.Punkte);
        }

        /// <summary>„Alles falsch" getippt: die eine falsche Aussage ist richtig beurteilt.</summary>
        [TestMethod]
        public void AnEmptySubmissionMeansEverythingIsFalse()
        {
            var frage = FrageAusDerMaske();

            var ergebnis = WahrFalschEvaluator.Werte(frage, [], 90);

            Assert.AreEqual(1, ergebnis.Richtige);
            Assert.AreEqual(30, ergebnis.Punkte);
            Assert.IsTrue(ergebnis.Urteile.All(u => !u.GetipptWahr));
        }

        [TestMethod]
        public void OneWrongJudgementCostsItsShare()
        {
            var frage = FrageAusDerMaske();

            var getippt = TastenDerWahren(frage).Take(1).ToList();

            var ergebnis = WahrFalschEvaluator.Werte(frage, getippt, 90);

            Assert.AreEqual(2, ergebnis.Richtige);
            Assert.AreEqual(60, ergebnis.Punkte);
        }

        [TestMethod]
        [DataRow(new[] { "A", "A" }, false, DisplayName = "doppelt")]
        [DataRow(new[] { "Z" }, false, DisplayName = "unbekannt")]
        [DataRow(new string[0], true, DisplayName = "alles falsch")]
        [DataRow(new[] { "A", "C" }, true, DisplayName = "zwei wahr")]
        public void TheServerChecksTheSubmission(string[] tasten, bool angenommen)
        {
            var spieler = new Player { Id = Guid.NewGuid(), Designation = "Anna" };
            var spiel = new Game { Id = Guid.NewGuid() };
            spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler });

            var state = new global::LocalBuzzer.Service.Base.States.BuzzerKeySelector(
                new global::LocalBuzzer.Service.Hubs.Accessors.GameAccessor { GetGame = () => spiel });

            state.Infos = new BuzzerKeySelectorInfo
            {
                KeysAndDesignations = new() { ["A"] = "a", ["B"] = "b", ["C"] = "c" },
                MaxAllowedSelections = 3,
                TrueFalse = true,
            };

            state.SetSelectedKeys(new SelectionResult { PlayerId = spieler.Id, Player = spieler, SelectedKeys = [.. tasten] });

            Assert.AreEqual(angenommen, state.KeyResultsForPlayer[spieler.Id].CommittedResult);
        }
    }
}
