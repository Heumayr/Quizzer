using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace Quizzer.DataModels
{
    /// <summary>
    /// Statische Konfigurationsklasse, die beim App-Start einmalig über <see cref="LoadSettings"/>
    /// aus <c>appsettings.json</c> befüllt wird. Stellt den Datenbankverbindungsstring und
    /// alle Pfade zu den benötigten Bild-Assets bereit. Alle Pfad-Properties sind
    /// berechnete Eigenschaften relativ zu <see cref="FilePathQuizzer"/>.
    /// </summary>
    public static class Settings
    {
        /// <summary>Das geladene <see cref="IConfiguration"/>-Objekt aus <c>appsettings.json</c>.</summary>
        public static IConfiguration Configuration { get; private set; } = null!;

        /// <summary>Wurzelverzeichnis für alle Quizzer-Assets (Bilder, Ressourcen). Aus <c>AppSettings:FilePathQuizzer</c>.</summary>
        public static string FilePathQuizzer { get; set; } = string.Empty;

        /// <summary>
        /// Alles, was Quizzer unter ProgramData ablegt (M28): darunter liegt der Datenordner
        /// <c>Daten</c>, und „Daten von diesem Rechner entfernen" leert genau diesen Ordner.
        /// Für Tests umlenkbar.
        /// </summary>
        public static string ProgramDataRoot { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Quizzer");

        /// <summary>
        /// Der Datenordner aus der Datei, mit aufgelösten Umgebungsvariablen. Die Vorgabe steht als
        /// <c>%ProgramData%\Quizzer\Daten</c> darin (M28) - ein fester Laufwerksbuchstabe passte nur
        /// auf einen einzigen Rechner.
        /// </summary>
        internal static string ResolveDataFolder(string eintrag) => Environment.ExpandEnvironmentVariables(eintrag);

        /// <summary>Ob <paramref name="ordner"/> unter <see cref="ProgramDataRoot"/> liegt.</summary>
        public static bool IsUnderProgramDataRoot(string? ordner)
        {
            if (string.IsNullOrWhiteSpace(ordner))
                return false;

            var wurzel = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProgramDataRoot)) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(ordner).StartsWith(wurzel, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Unterordner für Fragen-Ressourcen (Bilder, Videos, Audio).</summary>
        public static string ResourceRootFolder => Path.Combine(FilePathQuizzer, "Resources");

        /// <summary>Platzhalterbild für Audio-Ressourcen ohne eigenes Vorschaubild.</summary>
        public static string AudioPlaceholderFile => Path.Combine(FilePathQuizzer, "AudioPlaceholderFile.png");

        /// <summary>Hintergrundbild des Hauptfensters.</summary>
        public static string BackgroundImagePath => Path.Combine(FilePathQuizzer, "Background.png");

        /// <summary>Standard-Profilbild für Spieler ohne eigenes Bild.</summary>
        public static string PlaceholderPlayerImagePath => Path.Combine(FilePathQuizzer, "PlaceholderPlayer.png");

        /// <summary>Hintergrundbild der Spieler-Karte in der Ergebnisansicht.</summary>
        public static string PlayerCardBackgroundImagePath => Path.Combine(FilePathQuizzer, "PlayerCardBackground.png");

        /// <summary>Hintergrundbild der Spieler-Karte für den Gewinner.</summary>
        public static string PlayerCardBackgroundWinnerImagePath => Path.Combine(FilePathQuizzer, "PlayerCardBackgroundWinner.png");

        /// <summary>Hintergrundbild einer normalen Spielfeld-Zelle.</summary>
        public static string CellBackgroundImagePath => Path.Combine(FilePathQuizzer, "CellBackground.png");

        /// <summary>Hintergrundbild einer Spielfeld-Zelle im Hover-Zustand.</summary>
        public static string CellBackgroundHoverImagePath => Path.Combine(FilePathQuizzer, "CellBackgroundHover.png");

        /// <summary>Hintergrundbild einer bereits gespielten Spielfeld-Zelle.</summary>
        public static string CellBackgroundIsDoneImagePath => Path.Combine(FilePathQuizzer, "CellBackgroundIsDone.png");

        /// <summary>Hintergrundbild für Spalten-Header im Grid.</summary>
        public static string HeaderColumnBackgroundImagePath => Path.Combine(FilePathQuizzer, "HeaderColumnBackground.png");

        /// <summary>Hintergrundbild für Zeilen-Header im Grid.</summary>
        public static string HeaderRowBackgroundImagePath => Path.Combine(FilePathQuizzer, "HeaderRowBackground.png");

        /// <summary>Hintergrundbild des gesamten Spielfeld-Grids.</summary>
        public static string GridBackgroundImagePath => Path.Combine(FilePathQuizzer, "GridBackground.png");

        /// <summary>Hintergrundbild des Grids in der Ergebnisansicht.</summary>
        public static string GridBackgroundResultImagePath => Path.Combine(FilePathQuizzer, "GridBackgroundResult.png");

        /// <summary>Hintergrundbild für horizontale Layouts (z.B. Fragen-Anzeige im Landscape-Modus).</summary>
        public static string HorizontalBackgroundImagePath => Path.Combine(FilePathQuizzer, "HorizontalBackground.png");

        /// <summary>SQL Server-Verbindungsstring. Aus <c>AppSettings:MsSqlConString</c>.</summary>
        public static string ConnectionString { get; set; } = string.Empty;

        /// <summary>Der Datenordner, wenn keine <c>appsettings.json</c> ihn nennt (M28).</summary>
        internal const string DefaultFilePathQuizzer = @"%ProgramData%\Quizzer\Daten";

        /// <summary>Die Verbindung, wenn keine <c>appsettings.json</c> sie nennt - die LocalDB des Einzelbetriebs.</summary>
        internal const string DefaultConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer;Integrated Security=True";

        /// <summary>
        /// Lädt die Konfiguration aus <c>appsettings.json</c> im Ausgabe-Verzeichnis und
        /// befüllt alle statischen Properties. Muss einmalig beim App-Start aufgerufen werden
        /// (in <c>App.OnStartup</c> und im statischen Konstruktor von <c>DataContext</c>).
        /// </summary>
        public static void LoadSettings() => LoadSettingsFrom(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Wie <see cref="LoadSettings"/>, aus einem bestimmten Ordner. <b>Fehlt die Datei oder ein
        /// Eintrag, gelten die Vorgaben</b> (M28): die Datei steht in der <c>.gitignore</c>, ein
        /// frischer Klon hat keine - vorher brach der Start dann ab.
        /// </summary>
        internal static void LoadSettingsFrom(string ordner)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(ordner)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            FilePathQuizzer = ResolveDataFolder(Configuration["AppSettings:FilePathQuizzer"] ?? DefaultFilePathQuizzer);
            ConnectionString = Configuration["AppSettings:MsSqlConString"] ?? DefaultConnectionString;
        }
    }
}