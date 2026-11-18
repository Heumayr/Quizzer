using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Punkte-Parameter sind nie negativ (M37 P5, R3-3) - wie die Maske (<c>PositiveIntValidationRule</c>,
    /// <c>PositiveDoubleValidationRule</c>). Die eine Stelle für Bündel und Abgleich: ein negativer Wert machte den Feldwert
    /// negativ, und eine Teamregel teilte Minus aus.
    /// </summary>
    public static class Punktegrenzen
    {
        /// <summary>Faktoren und Zuschläge am Spiel, mit ihrer Beschriftung in der Maske.</summary>
        public static IReadOnlyDictionary<string, string> SpielFelder { get; } = new Dictionary<string, string>
        {
            [nameof(Game.DifficultyMultiplier)] = "Schwierigkeit Faktor",
            [nameof(Game.DifficultyAddition)] = "Schwierigkeit Zuschlag",
            [nameof(Game.DifficultyMinusMultiplier)] = "Minuspunkte Faktor",
            [nameof(Game.DifficultyMinusAddition)] = "Minuspunkte Zuschlag",
            [nameof(Game.PhaseMultiplier)] = "Phase Faktor",
            [nameof(Game.PhaseAddition)] = "Phase Zuschlag",
        };

        /// <summary>Die Punkte an der Frage, mit ihrer Beschriftung in der Maske.</summary>
        public static IReadOnlyDictionary<string, string> FrageFelder { get; } = new Dictionary<string, string>
        {
            [nameof(QuestionBase.Points)] = "Punkte",
            [nameof(QuestionBase.MinusPoints)] = "Minuspunkte",
        };

        /// <summary>Die Beanstandung eines Werts, oder <c>null</c>.</summary>
        public static string? Problem(string beschriftung, double wert)
            => wert < 0 ? $"„{beschriftung}“ darf nicht negativ sein ({wert})." : null;
    }
}
