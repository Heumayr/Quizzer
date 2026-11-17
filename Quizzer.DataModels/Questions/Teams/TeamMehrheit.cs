namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Wie die Antwort eines Teams bei der Team-Abstimmung zustande kam.</summary>
    public enum TeamMehrheitEntscheid
    {
        /// <summary>Eine Antwort hat die meisten Stimmen.</summary>
        Mehrheit = 0,

        /// <summary>Gleichstand - die Stimme des Sprechers lag unter den Gleichauf.</summary>
        Sprecher = 1,

        /// <summary>Gleichstand - die Spielleitung hat unter den Gleichauf gewählt (E1).</summary>
        Spielleitung = 2,

        /// <summary>Gleichstand ohne Sprecherstimme darunter - die Spielleitung muss entscheiden; bis dahin ist nichts bewertet.</summary>
        Offen = 3,
    }

    /// <summary>Was die Team-Abstimmung einem Team ergibt.</summary>
    /// <param name="Antwort">Die Antwort des Teams als Schlüssel (<see cref="TeamMehrheit.Schluessel"/>); <c>null</c>, solange ein Gleichstand offen ist.</param>
    /// <param name="Gleichauf">Die meistgewählten Antworten, geordnet - mehr als eine heißt Gleichstand.</param>
    /// <param name="Stimmen">Wie viele für jede der <paramref name="Gleichauf"/> gestimmt haben.</param>
    /// <param name="Abgegeben">Wie viele Mitglieder überhaupt abgestimmt haben.</param>
    /// <param name="Entscheid">Wie die Antwort zustande kam.</param>
    public sealed record TeamMehrheitErgebnis(string? Antwort, IReadOnlyList<string> Gleichauf, int Stimmen, int Abgegeben, TeamMehrheitEntscheid Entscheid)
    {
        public bool Gleichstand => Gleichauf.Count > 1;
    }

    /// <summary>
    /// Team-Abstimmung (M37 T7, Entwurf Abschnitt 3): jedes Mitglied stimmt am Telefon ab, die Mehrheit im Team ist seine
    /// Antwort. Eine Stimme ist die ganze Auswahl - bei Mehrfachauswahl zählen nur gleiche Auswahlen zusammen. Bei Gleichstand
    /// zählt die Stimme des Sprechers, wenn sie unter den Gleichauf ist; sonst entscheidet die Spielleitung, die auch einen
    /// Sprecherentscheid ändern kann (E1). Ohne Stimme gibt es keine Antwort - das Team wird nicht bewertet.
    /// </summary>
    public static class TeamMehrheit
    {
        /// <summary>Trennt die Tasten im Schlüssel einer Mehrfachauswahl.</summary>
        public const char Trenner = ',';

        /// <summary>
        /// Die Vergleichsform einer Auswahl: jede Taste einmal, geordnet - „C, A" und „A, C" sind dieselbe Stimme. Leer, wenn
        /// keine Taste gewählt ist; das ist keine Stimme.
        /// </summary>
        public static string Schluessel(IEnumerable<string?>? tasten)
            => string.Join(Trenner, (tasten ?? []).Select(t => t?.Trim() ?? string.Empty).Where(t => t.Length > 0)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        /// <summary>„A" bzw. „A + C" - wie die Antwort am Telefon und am Beamer steht.</summary>
        public static string Anzeige(string schluessel) => string.Join(" + ", schluessel.Split(Trenner, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>Die Antwort eines Teams; <c>null</c>, wenn kein Mitglied abgestimmt hat - dann wird das Team nicht bewertet.</summary>
        /// <param name="stimmen">Je Spieler seine Stimme als Schlüssel; wer nicht zum Team gehört oder leer stimmt, zählt nicht.</param>
        /// <param name="folge">Die Mitglieder des Teams.</param>
        /// <param name="sprecher">Der Sprecher dieser Frage - seine Stimme entscheidet einen Gleichstand, wenn sie darunter ist.</param>
        /// <param name="entscheid">Was die Spielleitung bei Gleichstand gewählt hat; zählt nur, wenn es unter den Gleichauf ist.</param>
        public static TeamMehrheitErgebnis? Ermittle(
            IReadOnlyDictionary<Guid, string> stimmen, IReadOnlyList<Guid> folge, Guid? sprecher, string? entscheid = null)
        {
            ArgumentNullException.ThrowIfNull(stimmen);
            ArgumentNullException.ThrowIfNull(folge);

            var mitglieder = folge.ToHashSet();
            var gueltig = stimmen.Where(s => mitglieder.Contains(s.Key) && !string.IsNullOrEmpty(s.Value)).ToDictionary();

            if (gueltig.Count == 0)
                return null;

            var zaehlung = gueltig.Values.GroupBy(s => s, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var meiste = zaehlung.Values.Max();
            var gleichauf = zaehlung.Where(z => z.Value == meiste).Select(z => z.Key).Order(StringComparer.Ordinal).ToList();

            if (gleichauf.Count == 1)
                return new(gleichauf[0], gleichauf, meiste, gueltig.Count, TeamMehrheitEntscheid.Mehrheit);

            if (entscheid != null && gleichauf.Contains(entscheid, StringComparer.Ordinal))
                return new(entscheid, gleichauf, meiste, gueltig.Count, TeamMehrheitEntscheid.Spielleitung);

            if (sprecher is { } id && gueltig.TryGetValue(id, out var sprecherstimme) && gleichauf.Contains(sprecherstimme, StringComparer.Ordinal))
                return new(sprecherstimme, gleichauf, meiste, gueltig.Count, TeamMehrheitEntscheid.Sprecher);

            return new(null, gleichauf, meiste, gueltig.Count, TeamMehrheitEntscheid.Offen);
        }
    }
}
