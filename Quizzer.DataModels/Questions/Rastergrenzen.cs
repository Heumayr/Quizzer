using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Raster und Phasen sind nie negativ (M37 P7, P5-4) - wie die Maske (<c>PositiveIntValidationRule</c>); 0 bleibt erlaubt, Spiele
    /// von vor der Phasenzahl tragen dort 0. Die eine Stelle für Bündel und Abgleich, nach dem Muster von <see cref="Punktegrenzen"/>:
    /// eine negative Zellengröße wirft im Spielfeld (<c>MatrixGridBehavior</c>, <c>RowDefinition.Height</c>).
    /// </summary>
    public static class Rastergrenzen
    {
        /// <summary>Raster und Phasen am Spiel, mit ihrer Beschriftung in der Maske.</summary>
        public static IReadOnlyDictionary<string, string> SpielFelder { get; } = new Dictionary<string, string>
        {
            [nameof(Game.Height)] = "Zeilen",
            [nameof(Game.Width)] = "Spalten",
            [nameof(Game.CellHeight)] = "Zellengröße Höhe",
            [nameof(Game.CellWidth)] = "Zellengröße Breite",
            [nameof(Game.SuggestedPhases)] = "Phasen im Abend",
        };
    }
}
