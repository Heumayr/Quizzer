namespace Quizzer.Server.Infrastructure
{
    /// <summary>
    /// Die Einstellungen des Servers, Abschnitt <c>Quizzer</c>. Im Container kommen sie aus
    /// Umgebungsvariablen (<c>Quizzer__Jwt__SigningKey</c> usw.) - Geheimnisse stehen nie in
    /// einer Datei im Repo.
    /// </summary>
    public sealed class ServerOptions
    {
        public const string Section = "Quizzer";

        public JwtOptions Jwt { get; set; } = new();

        /// <summary>Ablage der Medien. Im Container ein Volume.</summary>
        public string MediaRoot { get; set; } = "/data/media";

        /// <summary>Mindestlaenge neuer Kennwoerter (NIST 800-63B: Laenge vor Komplexitaet).</summary>
        public int PasswordMinLength { get; set; } = 12;

        /// <summary>Fehlversuche, nach denen ein Konto kurz gesperrt wird.</summary>
        public int LockoutThreshold { get; set; } = 5;

        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Groesste Mediendatei, die angenommen wird.</summary>
        public long MaxMediaBytes { get; set; } = 200L * 1024 * 1024;

        /// <summary>Erstes Admin-Konto - wird nur angelegt, solange es gar kein Konto gibt.</summary>
        public BootstrapOptions Bootstrap { get; set; } = new();

        /// <summary>Die SQL-Anmeldung, die <c>migrate</c> fuer den Betrieb anlegt.</summary>
        public AppLoginOptions AppLogin { get; set; } = new();
    }

    public sealed class JwtOptions
    {
        public string Issuer { get; set; } = "quizzer-server";

        public string Audience { get; set; } = "quizzer-wpf";

        /// <summary>Base64, mindestens 32 Byte. Alternativ <see cref="SigningKeyFile"/>.</summary>
        public string? SigningKey { get; set; }

        public string? SigningKeyFile { get; set; }

        public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
    }

    public sealed class BootstrapOptions
    {
        public string? AdminUser { get; set; }

        public string? AdminPassword { get; set; }
    }

    public sealed class AppLoginOptions
    {
        public string? Name { get; set; }

        public string? Password { get; set; }
    }
}
