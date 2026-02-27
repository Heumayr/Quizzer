using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Models.Enumerations;

namespace Quizzer.DataModels.Models
{
    public class Player : ModelBase
    {
        private PlayerConnection connectionState = PlayerConnection.Unknown;

        public event EventHandler<PlayerConnection>? PlayerConnectionChanged;

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

        [JsonIgnore]
        public PlayerConnection ConnectionState
        {
            get => connectionState;
            set
            {
                connectionState = value;
                PlayerConnectionChanged?.Invoke(this, value);
            }
        }

        public override string ToString()
        {
            if (DisplayName.Length > 0) return DisplayName;

            return Designation;
        }
    }
}