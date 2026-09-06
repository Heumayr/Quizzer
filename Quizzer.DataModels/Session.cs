using Quizzer.DataModels.Models.Base;
using System.Security.Cryptography;
using System.Text;

namespace Quizzer.DataModels
{
    /// <summary>
    /// Wer das Quiz gerade leitet.
    /// <para>
    /// Die Anmeldung entscheidet, welche Fragen zur Verfügung stehen: jeder Spielleiter sieht
    /// seine eigenen. Fragen ohne Besitzer gehören allen - das ist der Bestand aus der Zeit vor
    /// den Anmeldungen, und er soll niemandem verlorengehen.
    /// </para>
    /// <para>
    /// <b>Ein Kennwort ist vorgesehen, aber nicht erzwungen.</b> Wo keines gesetzt ist, genügt
    /// die Auswahl. Der Ableitungsweg steht trotzdem schon hier, damit ein Kennwort später nur
    /// noch gesetzt und nicht erst erfunden werden muss.
    /// </para>
    /// </summary>
    public static class Session
    {
        /// <summary>Der angemeldete Spielleiter; <c>null</c>, solange niemand angemeldet ist.</summary>
        public static Player? CurrentModerator { get; private set; }

        /// <summary>Die Kennung des angemeldeten Spielleiters, für Filter und neue Fragen.</summary>
        public static Guid? CurrentModeratorId => CurrentModerator?.Id;

        /// <summary>Ob überhaupt jemand angemeldet ist.</summary>
        public static bool IsSignedIn => CurrentModerator != null;

        /// <summary>Meldet einen Spielleiter an. Prüft das Kennwort, wenn eines gesetzt ist.</summary>
        /// <returns><c>true</c>, wenn die Anmeldung gelungen ist.</returns>
        public static bool SignIn(Player? moderator, string? password = null)
        {
            if (moderator == null)
                return false;

            if (!VerifyPassword(moderator, password))
                return false;

            CurrentModerator = moderator;

            return true;
        }

        /// <summary>Meldet ab. Danach steht wieder nichts zur Verfügung.</summary>
        public static void SignOut() => CurrentModerator = null;

        /// <summary>Ob dieser Spielleiter überhaupt ein Kennwort verlangt.</summary>
        public static bool RequiresPassword(Player? moderator)
            => !string.IsNullOrEmpty(moderator?.PasswordHash);

        /// <summary>
        /// Prüft ein Kennwort gegen die gespeicherte Ableitung. Ohne gesetztes Kennwort ist
        /// jede Eingabe richtig - auch keine.
        /// </summary>
        public static bool VerifyPassword(Player? moderator, string? password)
        {
            if (!RequiresPassword(moderator))
                return true;

            var teile = moderator!.PasswordHash.Split(':');

            if (teile.Length != 3 || !int.TryParse(teile[0], out var runden))
                return false;

            try
            {
                var salz = Convert.FromBase64String(teile[1]);
                var erwartet = Convert.FromBase64String(teile[2]);

                var berechnet = Ableiten(password ?? string.Empty, salz, runden, erwartet.Length);

                // Zeitkonstanter Vergleich: ein vorzeitiger Abbruch verriete, wie viele Zeichen
                // gestimmt haben.
                return CryptographicOperations.FixedTimeEquals(berechnet, erwartet);
            }
            catch (FormatException)
            {
                // Eine unlesbare Ableitung ist kein gueltiges Kennwort.
                return false;
            }
        }

        /// <summary>
        /// Bildet die speicherbare Form eines Kennworts: <c>Runden:Salz:Ableitung</c>, beides
        /// Base64. Ein leeres Kennwort ergibt eine leere Zeichenkette - also kein Kennwort.
        /// </summary>
        public static string HashPassword(string? password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            const int runden = 210_000;

            var salz = RandomNumberGenerator.GetBytes(16);
            var abgeleitet = Ableiten(password, salz, runden, 32);

            return $"{runden}:{Convert.ToBase64String(salz)}:{Convert.ToBase64String(abgeleitet)}";
        }

        private static byte[] Ableiten(string password, byte[] salz, int runden, int laenge)
            => Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salz, runden, HashAlgorithmName.SHA256, laenge);
    }
}
