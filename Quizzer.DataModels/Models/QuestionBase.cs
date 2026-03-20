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
    public class QuestionBase : ModelBase
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
                    IsFinish = true
                });
            }

            if (startSteps.Count == 0)
            {
                startSteps.Add(new QuestionStepResource
                {
                    IsStart = true
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
    }
}