using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Quizzer.DataModels.Models
{
    [Table(nameof(QuestionBase), Schema = "question")]
    public class QuestionBase : ModelBase<QuestionBase>
    {
        public string DesignationShort { get; set; } = string.Empty;

        public string QuestionText { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }

        public int Points { get; set; }

        public int MinusPoints { get; set; }

        public bool UseProportionalPointsPerStep { get; set; } = false;

        public string Notes { get; set; } = string.Empty;

        public QuestionType Typ { get; protected set; }

        public FinishType DefaultFinishType { get; protected set; } = FinishType.None;

        public Difficulty Difficulty { get; set; } = Difficulty.Level1;

        public bool WarnOnResultStep { get; set; } = true;

        public bool WarnOnFinishStep { get; set; } = true;

        public bool UseRandomSequenceOnNonFinishSteps { get; set; } = false;

        public QuestionViewKeyType QuestionViewKeyType { get; set; } = QuestionViewKeyType.Alphabetical;

        #region Buzzer

        public BuzzerControlsLayout BuzzerControlsLayout { get; set; } = BuzzerControlsLayout.Buzzer;

        public int BuzzerMaxAllowedKeySelect { get; set; } = 1;

        public bool ShowTextOnKeySelect { get; set; } = true;

        #endregion Buzzer

        #region View

        public StepDisplayLayoutMode StepDisplayLayoutMode { get; set; } = StepDisplayLayoutMode.Vertical;

        #endregion View

        public List<QuestionStepResource> Steps { get; set; } = new();

        public Category? Category { get; set; }

        [NotMapped]
        public QuestionStepResource[] OrderedSteps { get; set; } = [];

        public QuestionStepResource? GetNextStep(QuestionStepResource? currentStep = null)
        {
            if (!OrderedSteps.Any())
                return null;

            if (currentStep == null)
                return OrderedSteps.First();

            var seq = currentStep.SequenceNumber;
            return OrderedSteps.FirstOrDefault(s => s.SequenceNumber > seq);
        }

        public QuestionStepResource? GetStepBehind(QuestionStepResource? currentStep = null)
        {
            if (!OrderedSteps.Any() || currentStep == null)
                return null;

            var seq = currentStep.SequenceNumber;
            return OrderedSteps.Reverse().FirstOrDefault(s => s.SequenceNumber < seq);
        }

        public void CalculateOrderdSteps()
        {
            if (Steps == null || !Steps.Any())
            {
                OrderedSteps = [];
                return;
            }

            var result = new List<QuestionStepResource>();

            var normalSteps = Steps.Where(s => !s.IsFinish && !s.IsStart).ToList();
            var startSteps = Steps.Where(s => s.IsStart).ToList();
            var finishSteps = Steps.Where(s => s.IsFinish).ToList();

            if (UseRandomSequenceOnNonFinishSteps)
            {
                normalSteps = normalSteps
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();
            }
            else
            {
                normalSteps = normalSteps
                    .OrderBy(s => s.SequenceNumber)
                    .ToList();
            }

            if (finishSteps.Count == 0)
            {
                finishSteps.Add(new QuestionStepResource
                {
                    FinishType = DefaultFinishType,
                    IsFinish = true,
                    Id = Guid.NewGuid()
                });
            }

            if (startSteps.Count == 0)
            {
                startSteps.Add(new QuestionStepResource
                {
                    IsStart = true,
                    Id = Guid.NewGuid()
                });
            }

            var ordered = startSteps.Concat(normalSteps).Concat(finishSteps).ToList();

            var nextSequenceNumber = 0;
            var currentKey = Helpers.Helper.GetNextViewKey(string.Empty, QuestionViewKeyType);

            foreach (var step in ordered)
            {
                step.SequenceNumber = nextSequenceNumber;

                if (!step.IsStart && !step.IsFinish)
                {
                    step.QuestionViewKey = currentKey;
                    currentKey = Helpers.Helper.GetNextViewKey(currentKey, QuestionViewKeyType);
                }

                result.Add(step);
                nextSequenceNumber += 10;
            }

            OrderedSteps = result.ToArray();
        }

        protected virtual QuestionBase CreateCloneInstance()
        {
            return new QuestionBase();
        }

        public override QuestionBase CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = CreateCloneInstance();
            CopyQuestionBaseValuesTo(clone, copyIdentity);
            return clone;
        }

        protected void CopyQuestionBaseValuesTo(QuestionBase target, bool copyIdentity = true)
        {
            CopyBaseValuesTo(target, copyIdentity);

            target.DesignationShort = DesignationShort;
            target.QuestionText = QuestionText;
            target.CategoryId = CategoryId;
            target.Points = Points;
            target.MinusPoints = MinusPoints;
            target.UseProportionalPointsPerStep = UseProportionalPointsPerStep;
            target.Notes = Notes;
            target.Typ = Typ;
            target.DefaultFinishType = DefaultFinishType;
            target.Difficulty = Difficulty;
            target.WarnOnResultStep = WarnOnResultStep;
            target.WarnOnFinishStep = WarnOnFinishStep;
            target.UseRandomSequenceOnNonFinishSteps = UseRandomSequenceOnNonFinishSteps;
            target.QuestionViewKeyType = QuestionViewKeyType;

            target.BuzzerControlsLayout = BuzzerControlsLayout;
            target.BuzzerMaxAllowedKeySelect = BuzzerMaxAllowedKeySelect;
            target.ShowTextOnKeySelect = ShowTextOnKeySelect;

            target.StepDisplayLayoutMode = StepDisplayLayoutMode;

            target.Steps = new List<QuestionStepResource>();
            target.Category = null;
            target.OrderedSteps = [];
        }
    }
}