using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Ein Design: die Texturen und Farben, mit denen ein Spielabend gezeigt wird.
    /// <para>
    /// <b>Die Texturen liegen als Dateien, nicht in der Datenbank.</b> Ein Design zeigt über
    /// <see cref="FolderName"/> auf einen Unterordner von <c>Themes</c> im Datenverzeichnis. Wer
    /// eine Textur austauschen will, legt dort eine Datei mit dem passenden Namen ab -
    /// <c>Background.png</c>, <c>CellBackground.png</c> und so fort, genau die Namen, die das
    /// Programm schon immer verwendet.
    /// </para>
    /// <para>
    /// <b>Was der Ordner nicht enthält, kommt aus dem Auslieferungsstand.</b> Ein neues Design
    /// muss also nicht vollständig sein: wer nur den Hintergrund tauschen will, legt nur
    /// <c>Background.png</c> hinein und bekommt den Rest wie bisher. Ein Spiel ohne Design
    /// (<c>Game.GameThemeId == null</c>) sieht genau so aus wie vor der Einführung dieser
    /// Tabelle - das bestehende Design bleibt unangetastet.
    /// </para>
    /// </summary>
    [Table(nameof(GameTheme), Schema = "base")]
    public class GameTheme : ModelBase<GameTheme>
    {
        /// <summary>
        /// Unterordner unter <c>Themes</c> im Datenverzeichnis, in dem die Texturen liegen.
        /// Leer bedeutet: nur Farben, alle Texturen wie im Auslieferungsstand.
        /// </summary>
        public string FolderName { get; set; } = string.Empty;

        /// <summary>Grundfarbe der Fenster, als Hex (<c>#RRGGBB</c>). Leer heißt: unverändert.</summary>
        public string BackgroundColor { get; set; } = string.Empty;

        /// <summary>Schriftfarbe auf den Spielflächen, als Hex. Leer heißt: unverändert.</summary>
        public string ForegroundColor { get; set; } = string.Empty;

        /// <summary>Farbe der Zellbeschriftung im Spielfeld, als Hex. Leer heißt: unverändert.</summary>
        public string CellTextColor { get; set; } = string.Empty;

        /// <summary>Farbe der Spalten- und Zeilenköpfe, als Hex. Leer heißt: unverändert.</summary>
        public string HeaderTextColor { get; set; } = string.Empty;

        /// <summary>Kurze Beschreibung für die Auswahlliste.</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>Die Spiele, die dieses Design verwenden.</summary>
        public List<Game> Games { get; set; } = new();

        /// <summary>
        /// Der Name, unter dem das Design in der Auswahl erscheint. Ohne Bezeichnung wäre die
        /// Zeile in der Liste leer.
        /// </summary>
        [NotMapped]
        public string DisplayName => string.IsNullOrWhiteSpace(Designation) ? "Ohne Namen" : Designation;

        public override GameTheme CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new GameTheme
            {
                FolderName = FolderName,
                BackgroundColor = BackgroundColor,
                ForegroundColor = ForegroundColor,
                CellTextColor = CellTextColor,
                HeaderTextColor = HeaderTextColor,
                Notes = Notes,
                Games = new List<Game>(),
            };

            CopyBaseValuesTo(clone, copyIdentity);

            return clone;
        }
    }
}
