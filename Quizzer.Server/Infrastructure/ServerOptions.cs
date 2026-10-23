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

        /// <summary>
        /// Wo die KI-Einstellungen und der Tageszähler liegen (M31). Leer: <c>.ki</c> im
        /// <see cref="MediaRoot"/> - damit im selben Volume und in derselben Sicherung.
        /// </summary>
        public string? KiFolder { get; set; }

        /// <summary>Mindestlaenge neuer Kennwoerter (NIST 800-63B: Laenge vor Komplexitaet).</summary>
        public int PasswordMinLength { get; set; } = 12;

        /// <summary>Fehlversuche, nach denen ein Konto kurz gesperrt wird.</summary>
        public int LockoutThreshold { get; set; } = 5;

        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Groesste Mediendatei, die angenommen wird.</summary>
        public long MaxMediaBytes { get; set; } = 200L * 1024 * 1024;

        /// <summary>Wie viel die Medienablage insgesamt fassen darf (Vorgabe 20 GB).</summary>
        public long MaxMediaTotalBytes { get; set; } = 20L * 1024 * 1024 * 1024;

        /// <summary>Erstes Admin-Konto - wird nur angelegt, solange es gar kein Konto gibt.</summary>
        public BootstrapOptions Bootstrap { get; set; } = new();

        /// <summary>Die SQL-Anmeldung, die <c>migrate</c> fuer den Betrieb anlegt.</summary>
        public AppLoginOptions AppLogin { get; set; } = new();

        /// <summary>
        /// Netze, aus denen ein vorgeschalteter Proxy (Caddy) <c>X-Forwarded-For</c> und
        /// <c>X-Forwarded-Proto</c> setzen darf - etwa <c>172.30.77.0/24</c>, mehrere mit Komma.
        /// <para>
        /// <b>Leer heisst: keinem Kopf wird geglaubt.</b> Hinter Caddy ist das falsch - dann kaeme
        /// jede Anfrage von Caddys Adresse, und die Anmeldebremse je Adresse traefe alle
        /// gemeinsam. Ohne Proxy ist es richtig: dann koennte jeder Aufrufer sich per Kopf eine
        /// fremde Adresse geben und die Bremse umgehen.
        /// </para>
        /// </summary>
        public string? TrustedProxyNetworks { get; set; }
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
