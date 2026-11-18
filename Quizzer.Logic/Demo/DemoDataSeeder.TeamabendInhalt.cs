using Quizzer.DataModels.Enumerations;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Der Inhalt des Teamabends (M37 T12): vier Themen-Spalten zu je vier Zellen. Jede Regel, die das Teamspiel anbietet, und
    /// jede Art nur im Teamspiel steht genau einmal darin; dazu gewöhnliche Fragen (Team-Sperre, Joker-Runde) und Minispiele, die
    /// je Team zusammengefasst oder je Person werten (T8).
    /// <para>
    /// Eine neue Teamregel oder Teamart gehört hier hinein - <c>TeamabendUnitTests</c> prüft die Vollständigkeit über die
    /// Aufzählungen.
    /// </para>
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Die Spalten, links beginnend; je Zeile 100 Punkte mehr. Das Finale steht unten rechts - es kommt zuletzt.</summary>
        private static IEnumerable<AbendSpalte> BaueTeamabend(Guid kat)
        {
            yield return new("Aufwärmen", TeamAufwaermen(kat));
            yield return new("Gemeinsam", Gemeinsam(kat));
            yield return new("Team gegen Team", TeamGegenTeam(kat));
            yield return new("Wer gegen wen", TeamWerGegenWen(kat));
        }

        private static string Teamabend(string name) => $"{TeamabendName}: {name}";

        /// <summary>Gewöhnliche Fragen: wer antwortet, bekommt die Punkte (F-M37-08), nach falsch darf nur ein anderes Team (F-M37-09).</summary>
        private static List<AbendZelle> TeamAufwaermen(Guid kat) =>
        [
            new(Standard(kat, Teamabend("Wappentier von Wales"), "Wales", 100, Difficulty.Level0,
                "Welches Tier ist das Wappentier von Wales?",
                ["Es ist ein Fabelwesen.", "Es ist rot."],
                "Ein roter Drache")),
            new(MultipleChoice(kat, Teamabend("Volleyball"), "Volleyball", 200, Difficulty.Level0,
                "Wie viele Spieler einer Mannschaft stehen beim Volleyball auf dem Feld?",
                [("Sechs", true), ("Fünf", false), ("Sieben", false), ("Elf", false)],
                "Sechs.")),
            new(MultipleChoice(kat, Teamabend("Risiko"), "Ozean", 300, Difficulty.Level0,
                "Welcher Ozean ist der größte?",
                [("Pazifik", true), ("Atlantik", false), ("Indischer Ozean", false), ("Arktischer Ozean", false)],
                "Der Pazifik – größer als alle Landflächen der Erde zusammen."),
                CellSpecialRule.Risk),
            new(Standard(kat, Teamabend("Einsatz"), "Rhein", 400, Difficulty.Level0,
                "Welcher Fluss fließt am längsten durch Deutschland?",
                ["Er entspringt in der Schweiz.", "An ihm liegen Köln und Düsseldorf."],
                "Der Rhein"),
                CellSpecialRule.Stake, Angekuendigt: true),
        ];

        /// <summary>Die vier Teamregeln: das Team antwortet gemeinsam, der Wert geht aufgeteilt an die Mitglieder.</summary>
        private static List<AbendZelle> Gemeinsam(Guid kat) =>
        [
            new(Standard(kat, Teamabend("Teamberatung"), "Knochen", 100, Difficulty.Level0,
                "Wie viele Knochen hat ein erwachsener Mensch?",
                ["Es sind mehr als 200.", "Es sind weniger als 210."],
                "206"),
                CellSpecialRule.TeamCouncil),
            new(Standard(kat, Teamabend("Gleich gedacht"), "Gelbes Obst", 200, Difficulty.Level0,
                "Nennt ein Obst, das gelb ist – schreibt, was euer Team schreibt!",
                ["Denkt an den Obstkorb."],
                "Banane, Zitrone, Ananas, Quitte …"),
                CellSpecialRule.SameThought),
            new(Standard(kat, Teamabend("Alle oder keiner"), "Pisa", 300, Difficulty.Level0,
                "In welchem Land steht der Schiefe Turm von Pisa?",
                ["Das Land hat die Form eines Stiefels."],
                "Italien"),
                CellSpecialRule.AllOrNothing),
            new(MultipleChoice(kat, Teamabend("Team-Abstimmung"), "Spinne", 400, Difficulty.Level0,
                "Wie viele Beine hat eine Spinne?",
                [("Acht", true), ("Sechs", false), ("Zehn", false), ("Zwölf", false)],
                "Acht – Insekten haben sechs."),
                CellSpecialRule.TeamVote),
        ];

        /// <summary>Die Arten nur im Teamspiel, dazu die Reaktion: sie wertet je Team zusammengefasst (Durchschnitt oder Summe).</summary>
        private static List<AbendZelle> TeamGegenTeam(Guid kat) =>
        [
            new(OhneZeilen(kat, Teamabend("Tauziehen"), "Tauziehen", 100, Difficulty.Level0,
                "Team gegen Team: Wer zieht das Seil auf seine Seite? Tippen, so schnell ihr könnt!", MinigameKind.TugOfWar)),
            new(OhneZeilen(kat, Teamabend("Stellvertreter-Duell"), "Stellvertreter", 200, Difficulty.Level0,
                "Jedes Team schickt einen Stellvertreter – wer reagiert am schnellsten für sein Team?", MinigameKind.ProxyDuel)),
            new(MitZeilen(kat, Teamabend("Staffel"), "Staffel", 300, Difficulty.Level0,
                "Die Wochentage der Reihe nach, beginnend mit Montag – reihum je einer, ein Fehler gibt ans nächste Team weiter!",
                MinigameKind.Relay,
                [
                    ("Montag", false),
                    ("Dienstag", false),
                    ("Mittwoch", false),
                    ("Donnerstag", false),
                    ("Freitag", false),
                    ("Samstag", false),
                    ("Sonntag", false),
                ])),
            new(OhneZeilen(kat, Teamabend("Reaktion"), "Reaktion", 400, Difficulty.Level0,
                "Wer ist am schnellsten? Erst bei Grün tippen – im Teamspiel zählt das ganze Team!", MinigameKind.Reaction)),
        ];

        /// <summary>Minispiele mit Ziel - im Teamspiel nur Gegner, gewertet je Person -, unten das Finale (im Teamspiel antwortet der Sprecher).</summary>
        private static List<AbendZelle> TeamWerGegenWen(Guid kat) =>
        [
            new(OhneZeilen(kat, Teamabend("Räuber und Wächter"), "Räuber und Wächter", 100, Difficulty.Level0,
                "Bestehlen oder Schild? Alle wählen geheim und gleichzeitig – bestohlen wird nur ein anderes Team.", MinigameKind.RobberAndGuard)),
            new(OhneZeilen(kat, Teamabend("Glücksrad"), "Glücksrad", 200, Difficulty.Level0,
                "Das Glücksrad! Wer das Feld aufgedeckt hat, dreht.", MinigameKind.WheelOfFortune)),
            new(OhneZeilen(kat, Teamabend("Duell"), "Duell", 300, Difficulty.Level0,
                "Wer das Feld aufgedeckt hat, fordert jemanden aus einem anderen Team heraus!", MinigameKind.Duel)),
            new(Standard(kat, Teamabend("Finale"), "Australien", 400, Difficulty.Level0,
                "Wie heißt die Hauptstadt von Australien?",
                ["Es ist nicht Sydney.", "Sie wurde eigens als Hauptstadt geplant."],
                "Canberra"),
                CellSpecialRule.Final, Angekuendigt: true),
        ];
    }
}
