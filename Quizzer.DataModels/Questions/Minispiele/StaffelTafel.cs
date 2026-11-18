namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Tafel der Staffel am Beamer (M37 T11): je Team „✚ Rot – 2 Teile“ mit Balken, wer gerade läuft, steht dabei; darunter die
    /// gespielten Teile. Während der Runde steht nur, was gelöst ist - <b>ein verfehlter oder offener Teil zeigt seine Lösung erst am
    /// Ende</b>, ein anderes Team kann ihn noch lösen bzw. die Spielleitung die Antwort gelten lassen.
    /// </summary>
    public static class StaffelTafel
    {
        /// <summary>„1 Teil“ bzw. „3 Teile“.</summary>
        public static string Teile(int anzahl) => anzahl == 1 ? "1 Teil" : $"{anzahl} Teile";

        /// <summary>Je Team seine Teile; der Balken ist der Anteil an allen Teilen. Am Ende das Siegerteam markiert, sonst wer läuft.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Teams(StaffelStand stand, Func<Guid, string> teamName, Func<Guid, string> name, bool ende)
        {
            ArgumentNullException.ThrowIfNull(stand);
            ArgumentNullException.ThrowIfNull(teamName);
            ArgumentNullException.ThrowIfNull(name);

            var ergebnis = StaffelEvaluator.Werte(stand);

            return stand.Teams.Select(t =>
            {
                var teile = stand.GeloestVon(t.TeamId);
                var laeuft = !ende && stand.TeamAmZug == t.TeamId && stand.Laeufer is { } wer ? $" – {name(wer)} läuft" : string.Empty;

                return new ReaktionTafelZeile(teamName(t.TeamId), Teile(teile) + laeuft, ende && ergebnis.IstGewinner(t.TeamId),
                    stand.Teile > 0 ? (double)teile / stand.Teile : 0);
            }).ToList();
        }

        /// <summary>
        /// Die gespielten Teile, am Ende alle: „Teil 1: Merkur“ – „✚ Rot“. Ein Teil ohne Team heißt „verfehlt“; seine Lösung steht erst
        /// am Ende da. <paramref name="inhalt"/>: die Zeilen der Frage, wie der Server sie bekam.
        /// </summary>
        public static IReadOnlyList<ReaktionTafelZeile> Gespielt(
            StaffelStand stand, IReadOnlyList<MinispielZeile> inhalt, Func<Guid, string> teamName, bool ende)
        {
            ArgumentNullException.ThrowIfNull(stand);
            ArgumentNullException.ThrowIfNull(inhalt);
            ArgumentNullException.ThrowIfNull(teamName);

            var bis = ende ? Math.Min(stand.Teile, inhalt.Count) : Math.Min(stand.Teil, inhalt.Count);

            return Enumerable.Range(0, bis).Select(i => stand.Geloest[i] is { } team
                    ? new ReaktionTafelZeile($"Teil {i + 1}: {inhalt[i].Text}", teamName(team), false)
                    : new ReaktionTafelZeile(ende ? $"Teil {i + 1}: {inhalt[i].Text}" : $"Teil {i + 1}", "verfehlt", false))
                .ToList();
        }
    }
}
