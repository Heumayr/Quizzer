using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Anzeige
{
    /// <summary>
    /// Wie die Joker auf dem Bildschirm heißen (M35 C7) - eine Stelle für Spielleiterfenster und, ab C7b,
    /// das Telefon.
    /// </summary>
    public static class JokerTexte
    {
        public static string Name(JokerKind kind) => kind switch
        {
            JokerKind.Double => "Doppelt",
            JokerKind.Shield => "Schutz",
            JokerKind.Block => "Sperre",
            _ => "Unbekannter Joker",
        };

        public static string Wirkung(JokerKind kind) => kind switch
        {
            JokerKind.Double => "Das eigene Ergebnis dieser Frage zählt doppelt, Plus wie Minus.",
            JokerKind.Shield => "Das eigene Minus dieser Frage entfällt.",
            JokerKind.Block => "Ein gewählter Mitspieler darf diese Frage nicht beantworten.",
            _ => string.Empty,
        };
    }
}
