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

        /// <summary>
        /// Lädt die Konfiguration aus <c>appsettings.json</c> im Ausgabe-Verzeichnis und
        /// befüllt alle statischen Properties. Muss einmalig beim App-Start aufgerufen werden
        /// (in <c>App.OnStartup</c> und im statischen Konstruktor von <c>DataContext</c>).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Wenn <c>AppSettings:FilePathQuizzer</c> oder <c>AppSettings:MsSqlConString</c>
        /// in der Konfigurationsdatei fehlen.
        /// </exception>
        public static void LoadSettings()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            FilePathQuizzer = Configuration["AppSettings:FilePathQuizzer"]! ?? throw new InvalidOperationException("Missing FilePathQuestions");
            ConnectionString = Configuration["AppSettings:MsSqlConString"]! ?? throw new InvalidOperationException("Missing MsSqlConString");
        }
    }
}