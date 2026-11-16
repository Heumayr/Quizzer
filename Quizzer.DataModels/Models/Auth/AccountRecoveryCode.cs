using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Auth
{
    /// <summary>
    /// Ein Wiederherstellungscode fuer die Zwei-Faktor-Anmeldung (M36 A5). Gespeichert wird nur
    /// sein SHA-256; der Klartext geht einmal an den Nutzer. Wie <see cref="Account"/> nie im Abgleich.
    /// </summary>
    [Table(nameof(AccountRecoveryCode), Schema = "auth")]
    [Index(nameof(AccountId), nameof(CodeHash), IsUnique = true)]
    public class AccountRecoveryCode
    {
        [Key]
        public Guid Id { get; set; }

        public Guid AccountId { get; set; }

        [MaxLength(64)]
        public string CodeHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gesetzt, sobald der Code einmal angenommen wurde - danach gilt er nie wieder.</summary>
        public DateTimeOffset? UsedAt { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account Account { get; set; } = null!;
    }
}
