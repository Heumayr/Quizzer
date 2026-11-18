using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// Wer im Teamspiel zu welchem Team gehört (M37 T2) - die eine Stelle für Auslosen, Spielstart und den Stempel am
    /// Ergebnis. Rein: liest nur das geladene Spiel.
    /// </summary>
    public static class Teamzuordnung
    {
        /// <summary>Die Mitspieler ohne den Spielleiter: er ist nie im Los und braucht kein Team.</summary>
        public static IReadOnlyList<PlayerXGame> Teilnehmer(Game spiel) =>
            spiel.PlayerXGames.Where(x => x.PlayerId != spiel.ModeratorPlayerId).ToList();

        /// <summary>
        /// Der Stempel am Ergebnis (F-M37-07): das Team des Spielers beim Öffnen der Zelle. Nur im Teamspiel und nur ein
        /// Team dieses Spiels, sonst <c>null</c> - ein fremdes Team weist der Server ohnehin ab (<c>CheckTeamRefAsync</c>).
        /// </summary>
        public static Guid? TeamVon(Game spiel, Guid playerId)
        {
            if (!spiel.IsTeamGame)
                return null;

            var teamId = spiel.PlayerXGames.FirstOrDefault(x => x.PlayerId == playerId)?.GameTeamId;
            return teamId is { } id && spiel.GameTeams.Any(t => t.Id == id) ? id : null;
        }

        /// <summary>
        /// Ob zwei Spieler im selben Team spielen (M37 T8) - ohne Teamspiel nie; wer ohne Team ist, ist niemandes Teamkollege.
        /// Dieselbe Regel wie <c>MinigameRulesContext.Teamkollegen</c> am Hub, dessen Aufstellung hieraus entsteht.
        /// </summary>
        public static bool Teamkollegen(Game spiel, Guid a, Guid b) => TeamVon(spiel, a) is { } team && TeamVon(spiel, b) == team;

        /// <summary>Wie viele Lager unter den Teilnehmern sind (M37 T8): besetzte Teams; wer ohne Team ist, zählt für sich.</summary>
        public static int Lager(Game spiel) => Teilnehmer(spiel).Select(x => TeamVon(spiel, x.PlayerId) ?? x.PlayerId).Distinct().Count();

        /// <summary>Wie viele Teams unter den Teilnehmern besetzt sind (M37 T9) - wer ohne Team ist, zählt hier nicht.</summary>
        public static int BesetzteTeams(Game spiel) => Teilnehmer(spiel).Select(x => TeamVon(spiel, x.PlayerId)).OfType<Guid>().Distinct().Count();

        /// <summary>Teilnehmer ohne ein Team dieses Spiels.</summary>
        public static IReadOnlyList<PlayerXGame> OhneTeam(Game spiel) =>
            Teilnehmer(spiel).Where(x => x.GameTeamId is not { } id || spiel.GameTeams.All(t => t.Id != id)).ToList();

        /// <summary>Teams, denen kein Teilnehmer angehört.</summary>
        public static IReadOnlyList<GameTeam> LeereTeams(Game spiel)
        {
            var besetzt = Teilnehmer(spiel).Select(x => x.GameTeamId).ToHashSet();
            return spiel.GameTeams.Where(t => !besetzt.Contains(t.Id)).OrderBy(t => t.SortOrder).ToList();
        }

        /// <summary>Was den Start eines Teamspiels verhindert; leer ohne Teamspiel.</summary>
        public static IReadOnlyList<string> Startfehler(Game spiel)
        {
            if (!spiel.IsTeamGame)
                return [];

            var fehler = new List<string>();

            // R1-7: was vor der Prüfung an Abgleich und Bündel vorbeikam, hält spätestens hier an.
            if (TeamGrenzen.AufstellungProblem(spiel.GameTeams) is { } aufstellung)
                fehler.Add($"{aufstellung} Im Spielaufbau unter „Teams bearbeiten …“ ändern.");

            if (spiel.GameTeams.Count < TeamGrenzen.MinTeams)
                fehler.Add($"Ein Teamspiel braucht mindestens {TeamGrenzen.MinTeams} Teams, angelegt {(spiel.GameTeams.Count == 0 ? "ist keins" : "ist eins")}. "
                         + "Im Spielaufbau unter „Teams bearbeiten …“ anlegen oder auslosen.");
            else if (BesetzteTeams(spiel) < TeamGrenzen.MinTeams)
                // R3-H1: sonst laufen Teamregeln und zusammengefasste Minispiele ohne Gegner.
                fehler.Add($"Ein Teamspiel braucht mindestens {TeamGrenzen.MinTeams} Teams mit Mitgliedern, besetzt {(BesetzteTeams(spiel) == 0 ? "ist keins" : "ist eins")}. "
                         + "Im Spielaufbau unter „Teams bearbeiten …“ zuteilen oder auslosen.");

            if (OhneTeam(spiel) is { Count: > 0 } ohne)
                fehler.Add($"Ohne Team: {Namen(ohne)}. Im Spielaufbau unter „Teams bearbeiten …“ zuteilen oder auslosen.");

            return fehler;
        }

        /// <summary>Was am Abend auffallen kann, ohne den Start zu verhindern; leer ohne Teamspiel.</summary>
        public static IReadOnlyList<string> Startwarnungen(Game spiel)
        {
            if (!spiel.IsTeamGame)
                return [];

            return LeereTeams(spiel).Select(t => $"Team „{t.Name}“ hat keine Mitglieder.").ToList();
        }

        private static string Namen(IEnumerable<PlayerXGame> plaetze) =>
            string.Join(", ", plaetze.Select(x => x.Player?.CalculatedDisplayName ?? "(unbekannt)").OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
    }
}
