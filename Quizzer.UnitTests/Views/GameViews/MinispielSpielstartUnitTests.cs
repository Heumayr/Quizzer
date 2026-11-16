using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// V2 (M35 B2): die Spielstart-Pruefung kennt Minispiele - zu wenige Optionen und zu wenige
    /// Mitspieler fallen vor dem Abend auf, nicht vor den Gaesten.
    /// </summary>
    [TestClass]
    public class MinispielSpielstartUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void Setup()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void Cleanup() => UserPrompt.Reset();

        private static MinigameQuestion Mehrheit(int optionen)
        {
            var frage = new MinigameQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Essen",
                DesignationShort = "E",
                CategoryId = Guid.NewGuid(),
            };

            for (var i = 0; i < optionen; i++)
            {
                frage.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    QuestionBaseId = frage.Id,
                    SequenceNumber = (i + 1) * 10,
                    StepText = $"Option {i + 1}",
                    Designation = $"Option {i + 1}",
                });
            }

            return frage;
        }

        private static (List<string> Fehler, List<string> Warnungen) Befunde(MinigameQuestion frage, int spieler)
        {
            var fehler = new List<string>();
            var warnungen = new List<string>();

            GameMasterViewModel.Befunde(frage, spieler, fehler, warnungen);

            return (fehler, warnungen);
        }

        /// <summary>Eine Mehrheit mit einer Option ist beim Start ein Fehler - mit dem Satz fuer Fehler.</summary>
        [TestMethod]
        public void AMajorityWithOneOptionIsUnplayableAtTheStart()
        {
            var (fehler, _) = Befunde(Mehrheit(1), 3);

            Assert.AreEqual(1, fehler.Count, "Die einzige Option wird beim Start nicht beanstandet.");
            StringAssert.Contains(fehler[0], "mindestens 2 Antwortoptionen");

            GameMasterViewModel.StartFreigegeben(fehler, []);
            StringAssert.Contains(prompt.Confirms[^1].Message, "lässt sich nicht ordentlich spielen");
        }

        /// <summary>Die Mitspielerzahl kennt erst das Spiel - der Start meldet, wenn sie nicht reicht.</summary>
        [TestMethod]
        public void TooFewPlayersAreReportedAtTheStart()
        {
            var (fehler, _) = Befunde(Mehrheit(2), 1);

            Assert.AreEqual(1, fehler.Count, "Ein Minispiel mit einem einzigen Mitspieler wird nicht beanstandet.");
            StringAssert.Contains(fehler[0], "Mitspieler");
        }

        /// <summary>Die Gegenrichtung: eine spielbare Mehrheit meldet nichts - ohne Befund wird nicht gefragt.</summary>
        [TestMethod]
        public void APlayableMajorityReportsNothing()
        {
            var (fehler, warnungen) = Befunde(Mehrheit(3), 3);

            Assert.AreEqual(0, fehler.Count, string.Join(" | ", fehler));
            Assert.AreEqual(0, warnungen.Count, string.Join(" | ", warnungen));
        }
    }
}
