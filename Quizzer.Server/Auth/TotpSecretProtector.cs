using System.Security.Cryptography;
using System.Text;

namespace Quizzer.Server.Auth
{
    /// <summary>
    /// Verschluesselt das TOTP-Geheimnis mit AES-GCM; der Schluessel kommt per HKDF aus dem
    /// JWT-Signierschluessel mit eigenem Zweck. Wer den Signierschluessel wechselt, macht damit
    /// alle Einrichtungen unlesbar - sie muessen neu eingerichtet werden.
    /// </summary>
    public sealed class TotpSecretProtector
    {
        private const string Purpose = "quizzer-totp-v1";

        private const string Prefix = "v1:";

        private const int NonceBytes = 12;

        private const int TagBytes = 16;

        private readonly byte[] key;

        public TotpSecretProtector(TokenService tokens)
        {
            key = HKDF.DeriveKey(HashAlgorithmName.SHA256, tokens.SigningKey.Key, 32, salt: null, info: Encoding.UTF8.GetBytes(Purpose));
        }

        /// <summary>Die Konto-Id ist zugeordnete Daten: ein in eine fremde Zeile kopiertes Geheimnis entschluesselt nicht.</summary>
        public string Protect(Guid accountId, byte[] secret)
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
            var cipher = new byte[secret.Length];
            var tag = new byte[TagBytes];

            using (var aes = new AesGcm(key, TagBytes))
                aes.Encrypt(nonce, secret, cipher, tag, accountId.ToByteArray());

            return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        /// <summary><c>null</c>, wenn nichts gespeichert ist oder es sich nicht entschluesseln laesst.</summary>
        public byte[]? Unprotect(Guid accountId, string? stored)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
                return null;

            try
            {
                var data = Convert.FromBase64String(stored[Prefix.Length..]);
                if (data.Length <= NonceBytes + TagBytes)
                    return null;

                var secret = new byte[data.Length - NonceBytes - TagBytes];
                using var aes = new AesGcm(key, TagBytes);
                aes.Decrypt(data.AsSpan(0, NonceBytes), data.AsSpan(NonceBytes + TagBytes), data.AsSpan(NonceBytes, TagBytes), secret, accountId.ToByteArray());
                return secret;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                return null;
            }
        }
    }
}
