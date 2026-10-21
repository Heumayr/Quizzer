namespace Quizzer.Client
{
    /// <summary>
    /// Wo der Erneuerungsschluessel zwischen zwei Programmstarts liegt. Im WPF verschluesselt mit
    /// DPAPI fuer den angemeldeten Windows-Benutzer - nie im Klartext in <c>settings.json</c>.
    /// </summary>
    public interface ITokenStore
    {
        string? Load();

        void Save(string refreshToken);

        void Clear();
    }

    /// <summary>Nur im Speicher - fuer Tests und fuer „nicht angemeldet bleiben".</summary>
    public sealed class MemoryTokenStore : ITokenStore
    {
        private string? token;

        public string? Load() => token;

        public void Save(string refreshToken) => token = refreshToken;

        public void Clear() => token = null;
    }
}
