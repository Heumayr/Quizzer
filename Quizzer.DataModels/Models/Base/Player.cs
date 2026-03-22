using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Player), Schema = "base")]
    public class Player : ModelBase
    {
        public event EventHandler<PlayerConnection>? PlayerConnectionChanged;

        public string DisplayName { get; set; } = string.Empty;

        public List<QuestionResult> CurrentQuestionResults { get; set; } = new();

        public string UserPictureFileName { get; set; } = string.Empty;

        [NotMapped]
        private PlayerConnection connectionState = PlayerConnection.Unknown;

        [NotMapped]
        public string CalculatedDisplayName => string.IsNullOrEmpty(DisplayName) ? Designation : DisplayName;

        [NotMapped]
        public int FinalScore => Score - MinusScore;

        [NotMapped]
        public int Score => CurrentQuestionResults.Sum(qr => qr.Score);

        [NotMapped]
        public int MinusScore => CurrentQuestionResults.Sum(qr => qr.MinusScore);

        [NotMapped]
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