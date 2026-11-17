namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Eine Teamfarbe: Platz in der Palette, Name, Farbwert und Symbol - nie die Farbe allein (WCAG 1.4.1).</summary>
    /// <param name="Index">Der Wert in <c>GameTeam.ColorIndex</c>.</param>
    /// <param name="Name">„Orange" - im Spielaufbau und als Vorschlag für den Teamnamen.</param>
    /// <param name="Hex">Der Farbwert, <c>#RRGGBB</c>.</param>
    /// <param name="Symbol">Das Zeichen neben dem Namen, damit die Farbe nicht allein trägt.</param>
    public sealed record TeamFarbe(int Index, string Name, string Hex, string Symbol);

    /// <summary>
    /// Die feste Palette der Teams (M37 T1, Entwurf Abschnitt 2): die acht Farben nach Okabe-Ito, für
    /// Farbfehlsichtige unterscheidbar, je mit Name und Symbol. Eine freie Farbe gibt es nicht - der Server
    /// prüft nur den Platz, und weder Telefon noch Beamer setzen je einen fremden Farbwert.
    /// <para>
    /// <b>Plätze nur anhängen:</b> der Platz steht als Zahl in der Datenbank. Schwarz steht zuletzt, weil es auf
    /// dem dunklen Beamer am schlechtesten trägt.
    /// </para>
    /// </summary>
    public static class TeamFarben
    {
        public static IReadOnlyList<TeamFarbe> Alle { get; } =
        [
            new(0, "Orange", "#E69F00", "●"),
            new(1, "Himmelblau", "#56B4E9", "▲"),
            new(2, "Grün", "#009E73", "■"),
            new(3, "Gelb", "#F0E442", "◆"),
            new(4, "Blau", "#0072B2", "★"),
            new(5, "Rot", "#D55E00", "✚"),
            new(6, "Rosa", "#CC79A7", "⬟"),
            new(7, "Schwarz", "#000000", "⬢"),
        ];

        /// <summary>Ob es den Platz in der Palette gibt.</summary>
        public static bool IsValid(int index) => index >= 0 && index < Alle.Count;
    }
}
