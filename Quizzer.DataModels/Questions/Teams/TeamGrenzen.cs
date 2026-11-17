using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// Was ein Team und die Teameinstellungen eines Spiels tragen dürfen (M37 T1) - die eine Stelle für Server,
    /// Bündel und Spielaufbau. Eine Verletzung ist eine Zeile, die nicht passt, kein Rechteproblem: am Server
    /// <c>Invalid</c>, im Bündel ein abgewiesener Import.
    /// </summary>
    public static class TeamGrenzen
    {
        /// <summary>Längste Beratungszeit der Teamberatung in Sekunden; 0 heißt ohne Countdown.</summary>
        public const int MaxCouncilSeconds = 600;

        /// <summary>Ein Teamspiel braucht mindestens zwei Teams - mit einem gäbe es keinen Gegner (M37 T2).</summary>
        public const int MinTeams = 2;

        /// <summary>Höchstens so viele Teams, wie die Palette Farben hat - zwei Teams in einer Farbe verwechselt man.</summary>
        public static int MaxTeams => TeamFarben.Alle.Count;

        /// <summary>Was an einem Team nicht stimmt, oder <c>null</c>.</summary>
        public static string? TeamProblem(string? name, int colorIndex, int speakerTurn)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Ein Team braucht einen Namen.";

            if (name.Length > GameTeam.MaxNameLength)
                return $"Der Teamname ist länger als {GameTeam.MaxNameLength} Zeichen.";

            if (!TeamFarben.IsValid(colorIndex))
                return $"Die Teamfarbe {colorIndex} gibt es nicht – erlaubt sind 0 bis {TeamFarben.Alle.Count - 1}.";

            if (speakerTurn < 0)
                return "Der Sprecherzähler eines Teams darf nicht negativ sein.";

            return null;
        }

        /// <summary>Was an den Teameinstellungen eines Spiels nicht stimmt, oder <c>null</c>.</summary>
        public static string? SettingsProblem(TeamMinigameScoring scoring, int councilSeconds)
        {
            if (!Enum.IsDefined(scoring))
                return $"Unbekannte Wertung der Minispiele im Teamspiel ({(int)scoring}).";

            if (councilSeconds < 0 || councilSeconds > MaxCouncilSeconds)
                return $"Die Beratungszeit von {councilSeconds} s liegt außerhalb von 0 bis {MaxCouncilSeconds} s.";

            return null;
        }
    }
}
