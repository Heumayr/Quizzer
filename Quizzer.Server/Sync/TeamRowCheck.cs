using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.DataModels.Transfer.Sync;
using System.Text.Json;

namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Form der Teamzeilen (M37 T1, A08): Name, Farbe und Sprecherzähler am Team, Wertung und Beratungszeit am Spiel - nach
    /// <see cref="TeamGrenzen"/>, derselben Stelle wie Bündel und Spielaufbau. Eine Verletzung kostet nur diese Zeile
    /// (<c>Invalid</c>), wie die Längengrenze am Joker; der Rest des Pushs geht durch.
    /// </summary>
    internal static class TeamRowCheck
    {
        /// <summary>Die Beanstandung oder <c>null</c>; Zeilen anderer Typen gehen unbesehen durch.</summary>
        public static string? Pruefe(SyncRow row) => row.Type switch
        {
            // Ohne Namen ist ein Team ungültig, auch wenn das Feld ganz fehlt (fail-closed).
            nameof(GameTeam) => TeamGrenzen.TeamProblem(
                TextOf(row, nameof(GameTeam.Name)), IntOf(row, nameof(GameTeam.ColorIndex)) ?? 0, IntOf(row, nameof(GameTeam.SpeakerTurn)) ?? 0),

            // Fehlt ein Feld, bleibt der Stand der Zeile - geprüft wird, was kommt.
            nameof(Game) => TeamGrenzen.SettingsProblem(
                (TeamMinigameScoring)(IntOf(row, nameof(Game.TeamMinigameScoring)) ?? 0), IntOf(row, nameof(Game.TeamCouncilSeconds)) ?? 0),

            _ => null,
        };

        private static int? IntOf(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var zahl)
                ? zahl
                : null;

        private static string? TextOf(SyncRow row, string name)
            => row.Values.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
