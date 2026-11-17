using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Ein Team in der Rangfolge: Platz (Gleichstand teilt ihn), Team und Summe.</summary>
    public sealed record TeamPlatz(int Platz, GameTeam Team, int Summe);

    /// <summary>
    /// Die Teamwertung (M37 T3, Entwurf Abschnitt 3) - rein, liest nur das geladene Spiel. Die Summe selbst rechnet
    /// <see cref="Game.Teamstand"/>, die eine Stelle, nie gespeichert (A08). Hier steht, bei welchem Team ein Ergebnis
    /// zählt, und die Rangfolge. Minispiele je Team: <c>Teamwertung.Minispiel.cs</c> (M37 T8).
    /// </summary>
    public static partial class Teamwertung
    {
        /// <summary>
        /// Bei welchem Team ein Ergebnis zählt: beim Stempel (F-M37-07 - Umsetzen lässt die Punkte beim alten Team), ohne
        /// Stempel beim aktuellen Team des Spielers, ohne Team bei keinem (<c>null</c>). Ein Stempel, der kein Team dieses
        /// Spiels ist, zählt nirgends - auch nicht ersatzweise beim aktuellen Team.
        /// </summary>
        public static Guid? TeamDes(Game spiel, QuestionResult ergebnis)
        {
            if (ergebnis.GameTeamId is { } stempel)
                return spiel.GameTeams.Any(t => t.Id == stempel) ? stempel : null;

            return Teamzuordnung.TeamVon(spiel, ergebnis.PlayerId);
        }

        /// <summary>
        /// Die Teams nach Summe, Gleichstand teilt den Platz (1, 1, 3 - wie die Einzelwertung der Siegerehrung); bei
        /// gleicher Summe in der Reihenfolge des Spielaufbaus. Leer ohne Teamspiel.
        /// </summary>
        public static IReadOnlyList<TeamPlatz> Rangfolge(Game spiel)
        {
            if (!spiel.IsTeamGame)
                return [];

            var staende = spiel.GameTeams
                .Select(t => (Team: t, Summe: spiel.Teamstand(t.Id)))
                .OrderByDescending(x => x.Summe)
                .ThenBy(x => x.Team.SortOrder)
                .ToList();

            var plaetze = new List<TeamPlatz>(staende.Count);

            for (var i = 0; i < staende.Count; i++)
            {
                var platz = i > 0 && staende[i].Summe == staende[i - 1].Summe ? plaetze[i - 1].Platz : i + 1;
                plaetze.Add(new TeamPlatz(platz, staende[i].Team, staende[i].Summe));
            }

            return plaetze;
        }
    }
}
