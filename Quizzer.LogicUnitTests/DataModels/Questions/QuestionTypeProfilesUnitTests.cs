using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// Das Profil ist die einzige Stelle, an der steht, was ein Fragetyp mitbringt.
    /// Diese Tests halten fest, dass die Konstruktoren nicht daneben laufen.
    /// </summary>
    [TestClass]
    public class QuestionTypeProfilesUnitTests
    {
        [TestMethod]
        public void All_CoversEveryQuestionType()
        {
            var covered = QuestionTypeProfiles.All.Select(p => p.Typ).OrderBy(t => t).ToArray();
            var expected = Enum.GetValues<QuestionType>().OrderBy(t => t).ToArray();

            CollectionAssert.AreEqual(expected, covered,
                "Ein neuer Fragetyp ohne Profil faellt sonst erst im Betrieb auf.");
        }

        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.MultipleChoice)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void NewQuestion_MatchesItsProfile(QuestionType typ)
        {
            var question = Factory.CreateNewQuestion(typ);
            var profile = QuestionTypeProfiles.For(typ);

            Assert.AreEqual(typ, question.Typ);
            Assert.IsTrue(profile.MatchesOwnedValues(question),
                $"Der Konstruktor von {typ} weicht vom Profil ab.");
        }

        [TestMethod]
        public void For_RejectsUnknownType()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => QuestionTypeProfiles.For((QuestionType)99));
        }

        [TestMethod]
        public void MultipleChoice_UsesKeySelectAndGrid()
        {
            var profile = QuestionTypeProfiles.For(QuestionType.MultipleChoice);

            Assert.AreEqual(BuzzerControlsLayout.KeySelect, profile.BuzzerControlsLayout);
            Assert.AreEqual(StepDisplayLayoutMode.Grid, profile.StepDisplayLayoutMode,
                "Die Multiple-Choice-Ansicht kann nur Grid - alles andere zeigt Not Supported.");
            Assert.IsTrue(profile.UseRandomSequenceOnNoneFinishSteps);
            Assert.IsTrue(profile.ShowMaxAllowedKeySelect,
                "Die Hoechstzahl waehlbarer Antworten muss einstellbar bleiben.");
        }

        [TestMethod]
        public void Properties_UsesVerticalAndProportionalScoring()
        {
            var profile = QuestionTypeProfiles.For(QuestionType.Properties);

            Assert.AreEqual(StepDisplayLayoutMode.Vertical, profile.StepDisplayLayoutMode,
                "Die Eigenschaften-Ansicht kann nur Vertical.");
            Assert.IsTrue(profile.UseProportionalScoreReductionOnStep);
            Assert.AreEqual(QuestionViewKeyType.Numerical, profile.QuestionViewKeyType);
        }

        [TestMethod]
        public void Appreciate_UsesTheInputLayoutAndNeedsAnExpectedValue()
        {
            var profile = QuestionTypeProfiles.For(QuestionType.Appreciate);

            Assert.AreEqual(BuzzerControlsLayout.Input, profile.BuzzerControlsLayout);
            Assert.IsTrue(profile.ShowExpectedValue);
        }

        [TestMethod]
        public void ApplyTo_OverwritesValuesThatWereChangedByHand()
        {
            var question = Factory.CreateNewQuestion(QuestionType.MultipleChoice);
            question.StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical;
            question.UseRandomSequenceOnNoneFinishSteps = false;

            QuestionTypeProfiles.For(QuestionType.MultipleChoice).ApplyTo(question);

            Assert.AreEqual(StepDisplayLayoutMode.Grid, question.StepDisplayLayoutMode);
            Assert.IsTrue(question.UseRandomSequenceOnNoneFinishSteps);
        }

        [TestMethod]
        public void MatchesOwnedValues_NoticesAChangedLayoutMode()
        {
            var question = Factory.CreateNewQuestion(QuestionType.MultipleChoice);
            question.StepDisplayLayoutMode = StepDisplayLayoutMode.Horizontal;

            Assert.IsFalse(
                QuestionTypeProfiles.For(QuestionType.MultipleChoice).MatchesOwnedValues(question));
        }

        [TestMethod]
        public void EveryProfile_HasAGermanNameAndHelpText()
        {
            foreach (var profile in QuestionTypeProfiles.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(profile.DisplayName), $"{profile.Typ}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(profile.HelpText), $"{profile.Typ}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(profile.TableName), $"{profile.Typ}");
            }
        }
    }
}
