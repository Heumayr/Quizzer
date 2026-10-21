using Quizzer.Client;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Quizzer.Server
{
    /// <summary>
    /// Der Erneuerungsschluessel zwischen zwei Programmstarts - verschluesselt mit DPAPI fuer den
    /// angemeldeten Windows-Benutzer. Ein anderer Benutzer am selben Rechner, oder dieselbe Datei
    /// auf einem anderen Rechner, kann damit nichts anfangen.
    /// </summary>
    public sealed class DpapiTokenStore : ITokenStore
    {
        // Zusaetzliche Entropie: eine andere Anwendung desselben Benutzers, die DPAPI ohne diesen
        // Wert aufruft, entschluesselt die Datei nicht.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Quizzer.RefreshToken.v1");

        private readonly string path;

        public DpapiTokenStore(Uri server)
        {
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(server.GetLeftPart(UriPartial.Authority).ToLowerInvariant())))[..16];
            path = Path.Combine(ServerSession.LocalFolder, "session-" + name + ".bin");
        }

        public string? Load()
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                // Eine Datei, die sich nicht entschluesseln laesst, ist keine Sitzung.
                Clear();
                return null;
            }
        }

        public void Save(string refreshToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, cipher);
        }

        public void Clear()
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
