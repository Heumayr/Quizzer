using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// Jede Regel der Fragepruefung einzeln. Bis hierher liess sich eine unspielbare Frage
    /// speichern und fiel erst mitten im Spiel auf.
    /// </summary>
    [TestClass]
    public class QuestionValidatorUnitTests
    {
        private static QuestionStepResource Step(
            string designation = "Schritt",
            bool isResult = false,
            bool isFinish = false,
            bool isStart = false,
            string? resourceFileName = null,
            ResourceType resourceType = ResourceType.None)
            => new()
            {
                Id = Guid.NewGuid(),
                Designation = designation,
                StepText = designation,
                IsResult = isResult,
                IsFinish = isFinish,
                IsStart = isStart,
                ResourceFileName = resourceFileName ?? string.Empty,
                ResourceTyp = resourceType,
            };

        /// <summary>Eine Frage, die durchgeht - Ausgangspunkt fuer jede einzelne Verletzung.</summary>
        private static QuestionBase Valid(QuestionType typ = QuestionType.Default)
        {
            var question = Factory.CreateNewQuestion(typ);
            question.Designation = "Hauptstadt";
            question.DesignationShort = "HS";
            question.CategoryId = Guid.NewGuid();
            question.Points = 100;
            question.MinusPoints = 50;

            var profile = QuestionTypeProfiles.For(typ);

            for (var i = 0; i < Math.Max(profile.MinNormalSteps, 1); i++)
                question.Steps.Add(Step($"Option {i + 1}"));

            if (profile.RequiresResultStep)
            {
                question.Steps[0].IsResult = true;
                question.BuzzerMaxAllowedKeySelect = 1;
            }

            return question;
        }

        private static string[] CodesOf(QuestionBase question)
            => QuestionValidator.Validate(question).Select(i => i.Code).ToArray();

        [TestMethod]
        [DataRow(QuestionType.Default)]
        [DataRow(QuestionType.MultipleChoice)]
        [DataRow(QuestionType.Properties)]
        [DataRow(QuestionType.Appreciate)]
        public void AWellFormedQuestion_HasNoErrors(QuestionType typ)
        {
            var question = Valid(typ);

            var errors = QuestionValidator.Validate(question).Where(i => i.IsError).ToArray();

            Assert.AreEqual(0, errors.Length,
                "Unerwartet beanstandet: " + string.Join(", ", errors.Select(e => e.Code)));
            Assert.IsTrue(QuestionValidator.IsSavable(question));
        }

        [TestMethod]
        public void MissingDesignation_IsAnError()
        {
            var question = Valid();
            question.Designation = "   ";

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.DesignationMissing);
            Assert.IsFalse(QuestionValidator.IsSavable(question));
        }

        [TestMethod]
        public void MissingCategory_IsAnErrorInsteadOfADatabaseCrash()
        {
            var question = Valid();
            question.CategoryId = Guid.Empty;

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.CategoryMissing);
        }

        [TestMethod]
        public void MissingShortDesignation_IsOnlyAWarning()
        {
            var question = Valid();
            question.DesignationShort = string.Empty;

            var issue = QuestionValidator.Validate(question)
                .Single(i => i.Code == QuestionValidator.DesignationShortMissing);

            Assert.AreEqual(ValidationSeverity.Warning, issue.Severity);
            Assert.IsTrue(QuestionValidator.IsSavable(question),
                "Eine Warnung darf das Speichern nicht verhindern.");
        }

        [TestMethod]
        public void NegativePoints_AreAnError()
        {
            var question = Valid();
            question.Points = -1;
            question.MinusPoints = -1;

            var codes = CodesOf(question);

            CollectionAssert.Contains(codes, QuestionValidator.PointsNegative);
            CollectionAssert.Contains(codes, QuestionValidator.MinusPointsNegative);
        }

        [TestMethod]
        public void MultipleChoiceWithoutAnyResultStep_IsAnError()
        {
            var question = Valid(QuestionType.MultipleChoice);
            foreach (var step in question.Steps)
                step.IsResult = false;

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.ResultStepMissing);
        }

        [TestMethod]
        public void MultipleChoiceWithTooFewOptions_IsAnError()
        {
            var question = Valid(QuestionType.MultipleChoice);
            question.Steps.RemoveAt(question.Steps.Count - 1);

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.TooFewSteps);
        }

        /// <summary>
        /// Der Fall, der eine Frage unbeantwortbar macht: PlayerResultContext wertet nur als
        /// richtig, wenn die Anzahl gewaehlter Tasten der Hoechstzahl entspricht.
        /// </summary>
        [TestMethod]
        public void MultipleChoiceWithTwoAnswersButOneAllowedKey_IsAnError()
        {
            var question = Valid(QuestionType.MultipleChoice);
            question.Steps[0].IsResult = true;
            question.Steps[1].IsResult = true;
            question.BuzzerMaxAllowedKeySelect = 1;

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.KeySelectCountMismatch);
        }

        [TestMethod]
        public void MultipleChoiceWithTwoAnswersAndTwoAllowedKeys_IsFine()
        {
            var question = Valid(QuestionType.MultipleChoice);
            question.Steps[0].IsResult = true;
            question.Steps[1].IsResult = true;
            question.BuzzerMaxAllowedKeySelect = 2;

            Assert.IsTrue(QuestionValidator.IsSavable(question),
                "Mehrere richtige Antworten muessen moeglich sein.");
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void AllowedKeyCountBelowOne_IsAnError(int allowed)
        {
            var question = Valid(QuestionType.MultipleChoice);
            question.BuzzerMaxAllowedKeySelect = allowed;

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.KeySelectOutOfRange);
        }

        [TestMethod]
        public void AllowedKeyCountAboveTheNumberOfOptions_IsAnError()
        {
            var question = Valid(QuestionType.MultipleChoice);
            question.BuzzerMaxAllowedKeySelect = question.Steps.Count + 5;

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.KeySelectOutOfRange);
        }

        [TestMethod]
        public void AFileWithoutMediaType_IsAnError()
        {
            var question = Valid();
            question.Steps.Add(Step("mit Bild", resourceFileName: "berg.png"));

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.ResourceWithoutType);
        }

        [TestMethod]
        public void AMediaTypeWithoutFile_IsAnError()
        {
            var question = Valid();
            question.Steps.Add(Step("ohne Datei", resourceType: ResourceType.Image));

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.ResourceTypeWithoutFile);
        }

        [TestMethod]
        public void AFileWithMatchingMediaType_IsFine()
        {
            var question = Valid();
            question.Steps.Add(
                Step("mit Bild", resourceFileName: "berg.png", resourceType: ResourceType.Image));

            Assert.IsTrue(QuestionValidator.IsSavable(question));
        }

        [TestMethod]
        public void MoreThanOneStartStep_IsAnError()
        {
            var question = Valid();
            question.Steps.Add(Step("Start A", isStart: true));
            question.Steps.Add(Step("Start B", isStart: true));

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.MultipleStartSteps);
        }

        [TestMethod]
        public void MoreThanOneFinishStep_IsAnError()
        {
            var question = Valid();
            question.Steps.Add(Step("Ende A", isFinish: true));
            question.Steps.Add(Step("Ende B", isFinish: true));

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.MultipleFinishSteps);
        }

        [TestMethod]
        public void OneStartAndOneFinishStep_AreFine()
        {
            var question = Valid();
            question.Steps.Add(Step("Intro", isStart: true));
            question.Steps.Add(Step("Aufloesung", isFinish: true));

            Assert.IsTrue(QuestionValidator.IsSavable(question));
        }

        [TestMethod]
        public void AnEmptyStep_IsOnlyAWarning()
        {
            var question = Valid();
            question.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid() });

            var issue = QuestionValidator.Validate(question)
                .Single(i => i.Code == QuestionValidator.StepTextMissing);

            Assert.AreEqual(ValidationSeverity.Warning, issue.Severity);
        }

        /// <summary>
        /// Das Rueckfallnetz gegen die Anzeige Not Supported auf dem Spielerbildschirm.
        /// </summary>
        [TestMethod]
        public void ChangingATypeOwnedValue_IsAnError()
        {
            var question = Valid(QuestionType.MultipleChoice);
            question.StepDisplayLayoutMode = StepDisplayLayoutMode.Vertical;

            CollectionAssert.Contains(CodesOf(question), QuestionValidator.TypeOwnedValuesChanged);
        }

        [TestMethod]
        public void EveryIssue_CarriesAGermanSentence()
        {
            var question = Factory.CreateNewQuestion(QuestionType.MultipleChoice);
            question.StepDisplayLayoutMode = StepDisplayLayoutMode.Horizontal;

            foreach (var issue in QuestionValidator.Validate(question))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(issue.Message), issue.Code);
                Assert.IsFalse(string.IsNullOrWhiteSpace(issue.Code));
            }
        }

        [TestMethod]
        public void Validate_RejectsNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => QuestionValidator.Validate(null!));
        }
    }
}
