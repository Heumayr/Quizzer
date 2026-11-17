namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// „Teams auslosen“ (M37 T2, F-M37-05): Fisher-Yates über die Spieler mit eingereichter Zufallsquelle, danach reihum
    /// auf die Teams, deren Reihenfolge ebenfalls gemischt wird - die Größen unterscheiden sich höchstens um eins, und
    /// welches Team das größere ist, entscheidet das Los. Rein: wer mitlost, sagt <see cref="Teamzuordnung.Teilnehmer"/>.
    /// </summary>
    public static class Teamauslosung
    {
        /// <summary>Warum sich so viele Spieler nicht auf so viele Teams auslosen lassen, oder <c>null</c>.</summary>
        public static string? Problem(int spieler, int teams)
        {
            if (teams < TeamGrenzen.MinTeams)
                return $"Zum Auslosen braucht es mindestens {TeamGrenzen.MinTeams} Teams.";

            if (teams > TeamGrenzen.MaxTeams)
                return $"Höchstens {TeamGrenzen.MaxTeams} Teams – so viele Farben hat die Palette.";

            if (teams > spieler)
                return $"{teams} Teams brauchen mindestens {teams} Mitspieler, dabei {(spieler == 1 ? "ist einer" : $"sind {spieler}")}.";

            return null;
        }

        /// <summary>Lost die Spieler auf die Teams aus: Spieler-Id → Team-Id, jeder Spieler genau einmal.</summary>
        /// <exception cref="ArgumentException">Die Zahl der Teams passt nicht (<see cref="Problem"/>).</exception>
        public static IReadOnlyDictionary<Guid, Guid> Lose(IEnumerable<Guid> spieler, IEnumerable<Guid> teams, Random zufall)
        {
            var gemischt = spieler.Distinct().ToArray();
            var reihum = teams.Distinct().ToArray();

            if (Problem(gemischt.Length, reihum.Length) is { } problem)
                throw new ArgumentException(problem, nameof(teams));

            Mische(gemischt, zufall);
            Mische(reihum, zufall);

            var ergebnis = new Dictionary<Guid, Guid>(gemischt.Length);

            for (var i = 0; i < gemischt.Length; i++)
                ergebnis[gemischt[i]] = reihum[i % reihum.Length];

            return ergebnis;
        }

        private static void Mische(Guid[] liste, Random zufall)
        {
            for (var i = liste.Length - 1; i > 0; i--)
            {
                var j = zufall.Next(i + 1);
                (liste[i], liste[j]) = (liste[j], liste[i]);
            }
        }
    }
}
