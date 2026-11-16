using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>
    /// Was ein eingelöster Joker aus der eigenen Bewertung macht (M35 C7b, E1: nur ein Vorschlag auf der Karte).
    /// Doppelt verdoppelt Plus wie Minus, auch einen Teilwert, gekappt nach E2; Schutz macht ein Minus zu 0 und
    /// lässt das Plus stehen. Die Sperre ändert den eigenen Wert nicht - sie wirkt über die Antwortsperre.
    /// </summary>
    public static class JokerRegel
    {
        public static string DoppeltGrund => $"Joker: {JokerTexte.Name(JokerKind.Double)}";

        public static string SchutzGrund => $"Joker: {JokerTexte.Name(JokerKind.Shield)} – kein Minus";

        /// <summary>Der Vorschlag; <c>null</c>, wenn der Joker nicht greift (nicht bewertet, Plus beim Schutz, Sperre).</summary>
        public static Punktevorschlag? Werte(JokerKind art, SonderregelEingang eingang)
        {
            ArgumentNullException.ThrowIfNull(eingang);

            if (eingang.Bewertung == RatingKind.None)
                return null;

            return art switch
            {
                JokerKind.Double => Sonderregelwertung.Gekappt(2 * eingang.Grundwert, DoppeltGrund, eingang.Konto),
                JokerKind.Shield when eingang.Grundwert < 0 => new Punktevorschlag(0, SchutzGrund),
                _ => null,
            };
        }
    }
}
