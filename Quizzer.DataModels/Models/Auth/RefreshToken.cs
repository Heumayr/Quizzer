using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Auth
{
    /// <summary>
    /// Ein Erneuerungsschluessel. Gespeichert wird nur sein SHA-256, nie der Schluessel selbst.
    /// <para>
    /// Jede Erneuerung ersetzt ihn (<see cref="ReplacedById"/>). Taucht ein bereits ersetzter
    /// Schluessel wieder auf, ist er abgegriffen worden - dann wird die ganze
    /// <see cref="FamilyId"/> widerrufen, also auch der Nachfolger, den der Angreifer haelt.
    /// </para>
    /// </summary>
    [Table(nameof(RefreshToken), Schema = "auth")]
    [Index(nameof(TokenHash), IsUnique = true)]
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; }

        public Guid AccountId { get; set; }

        /// <summary>Alle Schluessel, die aus derselben Anmeldung hervorgegangen sind.</summary>
        public Guid FamilyId { get; set; }

        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public Guid? ReplacedById { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account Account { get; set; } = null!;
    }
}
