namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// Der Feldwert einer Teamfrage, auf die Mitglieder verteilt (M37 T5, F-M37-06): ganzzahlig zur Null hin, der Rest geht
    /// als ±1 an die ersten |Rest| der Sprecherfolge. 100 auf drei: 34/33/33, −100: −34/−33/−33. Die Summe ist immer der
    /// Wert - die Teamsumme wird nie gespeichert (Entwurf Abschnitt 0), also darf hier nichts verloren gehen.
    /// </summary>
    public static class Aufteilung
    {
        /// <summary>Je Mitglied sein Anteil; leer ohne Mitglieder. Doppelte Kennungen zählen einmal.</summary>
        public static IReadOnlyDictionary<Guid, int> Verteile(int wert, IEnumerable<Guid> sprecherfolge)
        {
            ArgumentNullException.ThrowIfNull(sprecherfolge);

            var mitglieder = sprecherfolge.Distinct().ToList();
            var anteile = new Dictionary<Guid, int>(mitglieder.Count);

            if (mitglieder.Count == 0)
                return anteile;

            // C# teilt zur Null hin, und der Rest trägt das Vorzeichen des Werts.
            var grund = wert / mitglieder.Count;
            var rest = wert % mitglieder.Count;

            for (var i = 0; i < mitglieder.Count; i++)
                anteile[mitglieder[i]] = grund + (i < Math.Abs(rest) ? Math.Sign(rest) : 0);

            return anteile;
        }
    }
}
