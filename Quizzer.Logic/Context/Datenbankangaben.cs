using Microsoft.Data.SqlClient;

namespace Quizzer.Logic.Context
{
    /// <summary>
    /// Die Verbindungszeichenfolge in einzelnen Feldern - und daraus wieder zusammengesetzt.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06 abends:</b> „datenbank einstellungen aufdroeseln und einzeln
    /// eingeben ... connectionstring wird zusammengebaut".
    /// </para>
    /// <para>
    /// <b>Gearbeitet wird auf dem <see cref="SqlConnectionStringBuilder"/> selbst</b>, nicht auf
    /// abgeschriebenen Feldern. Wer die Zeichenfolge aus sechs Eingaben neu baut, wirft alles weg,
    /// was sonst noch darinsteht - Zeitueberschreitung, Anwendungsname, MARS. Hier bleibt der
    /// Bestand stehen, und ueberschrieben wird nur, was die Maske wirklich anfasst.
    /// </para>
    /// </summary>
    public sealed class Datenbankangaben
    {
        /// <summary>Der Server, mit dem ein frisch ausgepacktes Programm anfaengt.</summary>
        public const string VorgabeServer = @"(localdb)\MSSQLLocalDB";

        /// <summary>Die Datenbank, mit der ein frisch ausgepacktes Programm anfaengt.</summary>
        public const string VorgabeDatenbank = "Quizzer";

        private readonly SqlConnectionStringBuilder bauer;

        private Datenbankangaben(SqlConnectionStringBuilder bauer) => this.bauer = bauer;

        /// <summary>
        /// Ob die uebergebene Zeichenfolge gelesen werden konnte. War sie es nicht, stehen hier
        /// die Vorgaben - <b>und das muss die Maske sagen</b>, sonst ueberschreibt ein
        /// arglosses „Speichern" eine Verbindung, die vielleicht nur ein Tippfehler war.
        /// </summary>
        public bool Unlesbar { get; private init; }

        /// <summary>
        /// Zerlegt eine Verbindungszeichenfolge. Eine unlesbare ergibt die Vorgaben und
        /// <see cref="Unlesbar"/>, keine Ausnahme - die Maske ist der einzige Weg zurueck, wenn
        /// die Verbindung kaputt ist, und darf daran nicht selbst scheitern.
        /// </summary>
        public static Datenbankangaben Zerlege(string? verbindung)
        {
            if (string.IsNullOrWhiteSpace(verbindung))
                return Vorgabe();

            try
            {
                return new Datenbankangaben(new SqlConnectionStringBuilder(verbindung));
            }
            catch (ArgumentException)
            {
                var angaben = Vorgabe();

                return new Datenbankangaben(angaben.bauer) { Unlesbar = true };
            }
        }

        /// <summary>Die ausgelieferte Vorgabe: oertliche LocalDB mit Windows-Anmeldung.</summary>
        public static Datenbankangaben Vorgabe()
            => new(new SqlConnectionStringBuilder
            {
                DataSource = VorgabeServer,
                InitialCatalog = VorgabeDatenbank,
                IntegratedSecurity = true,
            });

        /// <summary>Der Datenbankserver, etwa <c>(localdb)\MSSQLLocalDB</c> oder <c>SRV01\SQL</c>.</summary>
        public string Server
        {
            get => bauer.DataSource;
            set => bauer.DataSource = value ?? string.Empty;
        }

        /// <summary>Der Name der Datenbank auf diesem Server.</summary>
        public string Datenbank
        {
            get => bauer.InitialCatalog;
            set => bauer.InitialCatalog = value ?? string.Empty;
        }

        /// <summary>
        /// Anmeldung mit dem Windows-Konto statt mit Benutzer und Kennwort.
        /// <para>
        /// <b>Das Umschalten raeumt Benutzer und Kennwort weg.</b> Sonst bliebe ein Kennwort im
        /// Klartext in der Einstellungsdatei stehen, das gar nicht mehr gebraucht wird.
        /// </para>
        /// </summary>
        public bool Windowsanmeldung
        {
            get => bauer.IntegratedSecurity;
            set
            {
                bauer.IntegratedSecurity = value;

                if (!value)
                    return;

                // Entfernen statt leeren - ein "Password=;" in der Datei sieht aus wie ein
                // gesetztes leeres Kennwort und wirft die Frage auf, ob da eines fehlt.
                bauer.Remove("User ID");
                bauer.Remove("Password");
            }
        }

        /// <summary>Der Anmeldename, wenn nicht ueber Windows angemeldet wird.</summary>
        public string Benutzer
        {
            get => bauer.UserID;
            set => bauer.UserID = value ?? string.Empty;
        }

        /// <summary>Das Kennwort. Liegt im Klartext in der Einstellungsdatei - die Maske sagt das.</summary>
        public string Kennwort
        {
            get => bauer.Password;
            set => bauer.Password = value ?? string.Empty;
        }

        /// <summary>
        /// Dem Serverzertifikat vertrauen. Bei einem Server im Haus ohne eigenes Zertifikat ist
        /// das der Unterschied zwischen „verbindet" und „verbindet nicht".
        /// </summary>
        public bool ZertifikatVertrauen
        {
            get => bauer.TrustServerCertificate;
            set => bauer.TrustServerCertificate = value;
        }

        /// <summary>Die zusammengebaute Zeichenfolge - das, was gespeichert wird.</summary>
        public string Verbindung => bauer.ConnectionString;

        /// <summary>
        /// Dieselbe Zeichenfolge zum Anzeigen, mit unkenntlichem Kennwort. <b>Die Maske zeigt
        /// diese</b>, damit ein Blick ueber die Schulter das Kennwort nicht mitliest.
        /// </summary>
        public string VerbindungZumAnzeigen
        {
            get
            {
                if (string.IsNullOrEmpty(bauer.Password))
                    return bauer.ConnectionString;

                var sichtbar = new SqlConnectionStringBuilder(bauer.ConnectionString)
                {
                    Password = "********",
                };

                return sichtbar.ConnectionString;
            }
        }
    }
}
