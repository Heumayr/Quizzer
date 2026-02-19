using Newtonsoft.Json;
using Quizzer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Datamodels
{
    public class Player : ModelBase
    {
        public string DisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        public string CalculatedDisplayName => string.IsNullOrEmpty(DisplayName) ? Designation : DisplayName;

        [JsonIgnore]
        public int FinalScore => Score - MinusScore;

        [JsonIgnore]
        public int Score => CurrentQuestionResults.Sum(qr => qr.Score);

        [JsonIgnore]
        public int MinusScore => CurrentQuestionResults.Sum(qr => qr.MinusScore);

        [ClearOnSave]
        public List<QuestionResult> CurrentQuestionResults { get; set; } = new();

        public override string ToString()
        {
            if (DisplayName.Length > 0) return DisplayName;

            return Designation;
        }
    }
}