using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Auth
{
    /// <summary>
    /// Ein Anmeldekonto am Quizzer-Server, fest einem <see cref="Player"/> zugeordnet (M9: „user
    /// die auf die spieler gemappt werden"). Ein Mitspieler ohne Konto ist ein Gast.
    /// <para>
    /// <b>Wird nie abgeglichen.</b> Die Tabelle steht nur im Modell, damit Server und Client
    /// dieselben Migrationen fahren; in der lokalen Datenbank eines Clients bleibt sie leer, und
    /// der Abgleich (<c>SyncModel</c>) fuehrt den Typ nicht in seiner Positivliste.
    /// </para>
    /// </summary>
    [Table(nameof(Account), Schema = "auth")]
    [Index(nameof(NormalizedUserName), IsUnique = true)]
    [Index(nameof(PlayerId), IsUnique = true)]
    public class Account
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>Der Anmeldename, wie er eingegeben wurde.</summary>
        [MaxLength(64)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>Der Anmeldename in Kleinbuchstaben - gesucht wird nur hierueber.</summary>
        [MaxLength(64)]
        public string NormalizedUserName { get; set; } = string.Empty;

        /// <summary>PBKDF2-Ableitung aus <c>Session.HashPassword</c>. Verlaesst den Server nie.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        public AccountRole Role { get; set; } = AccountRole.Member;

        public Guid PlayerId { get; set; }

        public bool IsDisabled { get; set; }

        /// <summary>Fehlversuche seit der letzten erfolgreichen Anmeldung.</summary>
        public int FailedLoginCount { get; set; }

        /// <summary>Bis wann eine Anmeldung abgewiesen wird, egal mit welchem Kennwort.</summary>
        public DateTimeOffset? LockedUntil { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? LastLoginAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public Player Player { get; set; } = null!;

        public static string Normalize(string? userName) => (userName ?? string.Empty).Trim().ToLowerInvariant();
    }
}
