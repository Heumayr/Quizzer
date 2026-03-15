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
            if (!Steps.Any())
            {
                OrderedSteps = [];
                return;
            }

            var result = new List<QuestionStepResource>();

            var normalSteps = Steps.Where(s => !s.IsFinish);
            var finishSteps = Steps.Where(s => s.IsFinish);
            var nextSeqNumber = 0;
            var currentKey = Helpers.Helper.GetNextAlphabeticalEntry("");

            if (UseRandomSequenceOnNonFinishSteps)
            {
                var random = new Random((int)DateTime.Now.Ticks);

                foreach (var step in normalSteps)
                {
                    step.SequenceNumber = random.Next();
                }

                normalSteps = normalSteps.OrderBy(s => s.SequenceNumber);

                for (int i = 0; i < normalSteps.Count(); i++)
                {
                    normalSteps.ElementAt(i).SequenceNumber = nextSeqNumber;
                    normalSteps.ElementAt(i).QuestionViewKey = currentKey;
                    result.Add(normalSteps.ElementAt(i));
                    nextSeqNumber += 10;
                    currentKey = Helpers.Helper.GetNextAlphabeticalEntry(currentKey);
                }
            }
            else
            {
                foreach (var step in normalSteps.OrderBy(s => s.SequenceNumber))
                {
                    step.QuestionViewKey = currentKey;
                    result.Add(step);
                    currentKey = Helpers.Helper.GetNextAlphabeticalEntry(currentKey);
                }
            }

            if (!finishSteps.Any())
            {
                finishSteps.Append(new()
                {
                    FinishType = DefaultFinishType
                });
            }

            foreach (var step in finishSteps)
            {
                step.SequenceNumber = nextSeqNumber;
                step.QuestionViewKey = currentKey;
                result.Add(step);
                nextSeqNumber += 10;
                currentKey = Helpers.Helper.GetNextAlphabeticalEntry(currentKey);
            }

            OrderedSteps = result.ToArray();
        }
    }
}