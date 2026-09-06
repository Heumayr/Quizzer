using System.IO;

namespace Quizzer.DataModels.Themes
{
    /// <summary>
    /// Welche Texturen ein Design kennt und wo sie liegen.
    /// <para>
    /// <b>Die Dateinamen sind dieselben wie bisher.</b> Ein Design ist ein Unterordner unter
    /// <c>Themes</c> im Datenverzeichnis, in dem Dateien mit genau diesen Namen liegen dürfen.
    /// Was dort fehlt, kommt aus dem Datenverzeichnis selbst - also aus dem Auslieferungsstand.
    /// Deshalb muss ein neues Design nie vollständig sein, und deshalb sieht ein Spiel ohne
    /// Design aus wie immer.
    /// </para>
    /// </summary>
    public static class ThemeAssets
    {
        /// <summary>Eine austauschbare Textur.</summary>
        /// <param name="Schluessel">Kurzname, unter dem der Editor sie führt.</param>
        /// <param name="Dateiname">Der Dateiname im Design-Ordner.</param>
        /// <param name="Beschriftung">Was der Spielleiter im Editor liest.</param>
        /// <param name="Erklaerung">Wo die Textur im Spiel auftaucht.</param>
        public sealed record Textur(string Schluessel, string Dateiname, string Beschriftung, string Erklaerung);

        /// <summary>Alle austauschbaren Texturen, in der Reihenfolge des Editors.</summary>
        public static IReadOnlyList<Textur> Texturen { get; } =
        [
            new("Hintergrund", "Background.png", "Hintergrund",
                "Der Grund hinter dem Spielfeld, auf dem Beamer und beim Spielleiter."),
            new("Zelle", "CellBackground.png", "Zelle",
                "Eine offene Zelle im Spielfeld."),
            new("ZelleHover", "CellBackgroundHover.png", "Zelle unter dem Zeiger",
                "Die Zelle, über der die Maus steht."),
            new("ZelleGespielt", "CellBackgroundIsDone.png", "Zelle gespielt",
                "Eine Zelle, die schon abgeschlossen ist."),
            new("SpaltenKopf", "HeaderColumnBackground.png", "Spaltenkopf",
                "Die Kategorienzeile über dem Spielfeld."),
            new("ZeilenKopf", "HeaderRowBackground.png", "Zeilenkopf",
                "Die Punktespalte links vom Spielfeld."),
            new("Auswahlfeld", "GridBackground.png", "Antwortfeld",
                "Eine Antwortmöglichkeit bei einer Multiple-Choice-Frage."),
            new("AuswahlfeldRichtig", "GridBackgroundResult.png", "Antwortfeld als Lösung",
                "Die aufgedeckte richtige Antwort."),
            new("Schrittleiste", "HorizontalBackground.png", "Schrittleiste",
                "Der Grund hinter den Frageschritten."),
            new("Spielerkarte", "PlayerCardBackground.png", "Spielerkarte",
                "Die Karte eines Mitspielers im Punktestand."),
            new("SpielerkarteSieger", "PlayerCardBackgroundWinner.png", "Spielerkarte des Siegers",
                "Die Karte dessen, der die Runde gewonnen hat."),
            new("SpielerPlatzhalter", "PlaceholderPlayer.png", "Platzhalterbild",
                "Steht für einen Mitspieler ohne eigenes Bild."),
        ];

        /// <summary>Das Verzeichnis, unter dem alle Designs liegen.</summary>
        public static string ThemesRoot => Path.Combine(Settings.FilePathQuizzer, "Themes");

        /// <summary>Das Verzeichnis eines einzelnen Designs.</summary>
        public static string FolderOf(string? folderName)
            => string.IsNullOrWhiteSpace(folderName)
                ? string.Empty
                : Path.Combine(ThemesRoot, folderName);

        /// <summary>
        /// Der Pfad, unter dem eine Textur wirklich zu finden ist.
        /// <para>
        /// Erst im Design-Ordner, dann im Datenverzeichnis. Gibt es beides nicht, kommt der Pfad
        /// im Datenverzeichnis zurück - der Aufrufer muss ohnehin mit einer fehlenden Datei
        /// zurechtkommen, und so steht in einer Fehlermeldung der Ort, an den die Datei gehört.
        /// </para>
        /// </summary>
        public static string Resolve(string? folderName, string dateiname)
        {
            var ordner = FolderOf(folderName);

            if (!string.IsNullOrEmpty(ordner))
            {
                var eigen = Path.Combine(ordner, dateiname);

                if (File.Exists(eigen))
                    return eigen;
            }

            return Path.Combine(Settings.FilePathQuizzer, dateiname);
        }

        /// <summary>
        /// Liefert zu jeder Textur, ob das Design sie selbst mitbringt. Der Editor zeigt damit
        /// auf einen Blick, was eigen ist und was aus dem Auslieferungsstand kommt.
        /// </summary>
        public static bool IsOwn(string? folderName, string dateiname)
        {
            var ordner = FolderOf(folderName);

            return !string.IsNullOrEmpty(ordner) && File.Exists(Path.Combine(ordner, dateiname));
        }
    }
}
