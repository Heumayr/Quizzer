using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Quizzer.DataModels.Models
{
    [Table(nameof(QuestionBase), Schema = "question")]
    public class QuestionBase : ModelBase<QuestionBase>
    {
        public virtual string DesignationShort { get; set; } = string.Empty;

        public virtual string QuestionText { get; set; } = string.Empty;

        public virtual Guid CategoryId { get; set; }

        public virtual int Points { get; set; }

        public virtual int MinusPoints { get; set; }

        public virtual string Notes { get; set; } = string.Empty;

        public virtual QuestionType Typ { get; protected set; }

        public virtual FinishType DefaultFinishType { get; protected set; } = FinishType.None;

        public virtual Difficulty Difficulty { get; set; } = Difficulty.Level1;

        public virtual bool WarnOnResultStep { get; set; } = true;

        public virtual bool WarnOnFinishStep { get; set; } = true;

        public virtual bool UseRandomSequenceOnNonFinishSteps { get; set; } = false;

        #region Buzzer

        public virtual BuzzerControlsLayout BuzzerControlsLayout { get; set; } = BuzzerControlsLayout.Buzzer;

        public virtual int BuzzerMaxAllowedKeySelect { get; set; } = 1;

        public virtual bool ShowTextOnKeySelect { get; set; } = true;

        #endregion Buzzer

        public List<QuestionStepResource> Steps { get; set; } = new List<QuestionStepResource>();

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
            if (!OrderedSteps.Any())
                return null;

            if (currentStep == null)
                return null;

            var reverse = OrderedSteps.Reverse();

            var seq = currentStep.SequenceNumber;

            return reverse.FirstOrDefault(s => s.SequenceNumber < seq);
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

            var ordered = startSteps.Concat(normalSteps.Concat(finishSteps)).ToList();

            var nextSequenceNumber = 0;
            var currentKey = Helpers.Helper.GetNextAlphabeticalEntry("");

            foreach (var step in ordered)
            {
                step.SequenceNumber = nextSequenceNumber;

                if (!step.IsStart && !step.IsFinish)
                {
                    step.QuestionViewKey = currentKey;
                    currentKey = Helpers.Helper.GetNextAlphabeticalEntry(currentKey);
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
            target.Notes = Notes;
            target.Difficulty = Difficulty;
            target.WarnOnResultStep = WarnOnResultStep;
            target.WarnOnFinishStep = WarnOnFinishStep;
            target.UseRandomSequenceOnNonFinishSteps = UseRandomSequenceOnNonFinishSteps;

            target.Steps = new List<QuestionStepResource>();
            target.Category = null;
            target.OrderedSteps = [];
        }
    }
}