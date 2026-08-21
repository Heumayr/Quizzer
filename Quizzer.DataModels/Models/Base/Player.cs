using Newtonsoft.Json;
using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Repräsentiert einen Spieler. Speichert Profildaten (Name, Bild) sowie
    /// die Ergebnisse der aktuellen Spielsitzung. Der Verbindungsstatus zum
    /// Buzzer-Server wird zur Laufzeit über <see cref="ConnectionState"/> verwaltet
    /// und nicht in der Datenbank persistiert. Gespeichert in <c>base.Player</c>.
    /// </summary>
    [Table(nameof(Player), Schema = "base")]
    public class Player : ModelBase<Player>
    {
        /// <summary>
        /// Wird ausgelöst, wenn sich <see cref="ConnectionState"/> ändert.
        /// Ermöglicht der WPF-Oberfläche, auf Verbindungsänderungen zu reagieren.
        /// </summary>
        public event EventHandler<PlayerConnection>? PlayerConnectionChanged;

        /// <summary>
        /// Angezeigter Spielername (frei wählbar). Wenn leer, wird <c>Designation</c> verwendet.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Ergebnisse aller bisher beantworteten Fragen in der aktuellen Spielsitzung.</summary>
        public List<QuestionResult> CurrentQuestionResults { get; set; } = new();

        /// <summary>Dateiname des Spieler-Profilbilds (relativ zu <c>Settings.FilePathQuizzer</c>).</summary>
        public string UserPictureFileName { get; set; } = string.Empty;

        /// <summary>Backing-Field für <see cref="ConnectionState"/>; nicht in der DB gespeichert.</summary>
        [NotMapped]
        private PlayerConnection connectionState = PlayerConnection.Unknown;

        /// <summary>
        /// Gibt <see cref="DisplayName"/> zurück, falls gesetzt; andernfalls <c>Designation</c>.
        /// </summary>
        [NotMapped]
        public string CalculatedDisplayName => string.IsNullOrEmpty(DisplayName) ? Designation : DisplayName;

        /// <summary>Netto-Punktestand: Punkte minus Minuspunkte aus allen Fragenresultaten.</summary>
        [NotMapped]
        public int FinalScore => Score - MinusScore;

        /// <summary>Summe aller erzielten Punkte aus <see cref="CurrentQuestionResults"/>.</summary>
        [NotMapped]
        public int Score => CurrentQuestionResults.Sum(qr => qr.Score);

        /// <summary>Summe aller abgezogenen Punkte aus <see cref="CurrentQuestionResults"/>.</summary>
        [NotMapped]
        public int MinusScore => CurrentQuestionResults.Sum(qr => qr.MinusScore);

        /// <summary>
        /// Aktueller Verbindungsstatus des Spielers zum Buzzer-Server.
        /// Beim Setzen wird <see cref="PlayerConnectionChanged"/> ausgelöst.
        /// Wird nicht in der Datenbank gespeichert.
        /// </summary>
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

        /// <summary>Gibt <see cref="DisplayName"/> zurück, falls gesetzt; andernfalls <c>Designation</c>.</summary>
        public override string ToString()
        {
            if (DisplayName.Length > 0) return DisplayName;

            return Designation;
        }

        /// <inheritdoc/>
        public override Player CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new Player
            {
                DisplayName = DisplayName,
                CurrentQuestionResults = new List<QuestionResult>(),
                UserPictureFileName = UserPictureFileName,
                ConnectionState = ConnectionState
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}