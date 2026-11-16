using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Quizzer.Server.Auth
{
    /// <summary>
    /// TOTP nach RFC 6238 (M36 A5): HMAC-SHA1, 30 s, 6 Ziffern - selbst gebaut statt eines Pakets.
    /// SHA1 ist hier keine freie Wahl: Authenticator-Apps erwarten es, und als HMAC ist es nicht gebrochen.
    /// </summary>
    public static class Totp
    {
        public const int StepSeconds = 30;

        public const int Digits = 6;

        public const int SecretBytes = 20;

        /// <summary>Ein Schritt vor und zurueck - fuer Uhren, die ein wenig abweichen.</summary>
        public const int Window = 1;

        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

        public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

        /// <summary>Der Code eines Zeitschritts (RFC 4226, dynamische Kuerzung).</summary>
        public static string Code(byte[] secret, long step, int digits = Digits)
        {
            Span<byte> counter = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counter, step);

            Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
            HMACSHA1.HashData(secret, counter, hash);

            var offset = hash[^1] & 0x0F;
            var binary = (hash[offset] & 0x7F) << 24 | hash[offset + 1] << 16 | hash[offset + 2] << 8 | hash[offset + 3];
            var modulo = (int)Math.Pow(10, digits);

            return (binary % modulo).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
        }

        /// <summary>Sechs Ziffern, Leerzeichen dazwischen erlaubt - alles andere ist kein TOTP-Code.</summary>
        public static bool IsCodeFormat(string? code)
        {
            var cleaned = Clean(code);
            return cleaned.Length == Digits && cleaned.All(char.IsAsciiDigit);
        }

        /// <summary>
        /// Der hoechste Schritt im Fenster, zu dem der Code passt und der nach <paramref name="lastStep"/>
        /// liegt; sonst <c>null</c>. Derselbe oder ein frueherer Schritt ist eine Wiederholung.
        /// </summary>
        public static long? Match(byte[] secret, string? code, DateTimeOffset now, long? lastStep)
        {
            if (!IsCodeFormat(code))
                return null;

            var given = Encoding.ASCII.GetBytes(Clean(code));
            var current = StepAt(now);
            long? matched = null;

            // Alle Schritte des Fensters werden verglichen, ohne frueh auszusteigen - konstante Zeit.
            for (var step = current - Window; step <= current + Window; step++)
            {
                var expected = Encoding.ASCII.GetBytes(Code(secret, step));
                var equal = CryptographicOperations.FixedTimeEquals(expected, given);

                if (equal && (lastStep == null || step > lastStep))
                    matched = step;
            }

            return matched;
        }

        /// <summary>Die Angabe fuer den QR-Code (Key-URI-Format der Authenticator-Apps).</summary>
        public static string KeyUri(string issuer, string accountName, string base32Secret)
            => $"otpauth://totp/{Uri.EscapeDataString(issuer + ":" + accountName)}"
               + $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

        /// <summary>Base32 nach RFC 4648, ohne Auffuellzeichen.</summary>
        public static string ToBase32(ReadOnlySpan<byte> data)
        {
            var result = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bits = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;

                while (bits >= 5)
                {
                    result.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }

            if (bits > 0)
                result.Append(Alphabet[(buffer << (5 - bits)) & 31]);

            return result.ToString();
        }

        public static byte[] FromBase32(string text)
        {
            var cleaned = Clean(text).TrimEnd('=').ToUpperInvariant();
            var result = new List<byte>(cleaned.Length * 5 / 8);
            int buffer = 0, bits = 0;

            foreach (var c in cleaned)
            {
                var value = Alphabet.IndexOf(c);
                if (value < 0)
                    throw new FormatException($"'{c}' gehört nicht zu Base32.");

                buffer = (buffer << 5) | value;
                bits += 5;

                if (bits >= 8)
                {
                    result.Add((byte)(buffer >> (bits - 8)));
                    bits -= 8;
                }
            }

            return result.ToArray();
        }

        private static string Clean(string? text)
            => new((text ?? string.Empty).Where(c => c is not (' ' or '-')).ToArray());
    }
}
