using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Arten nur im Teamspiel (M37 T9 ff.): Tauziehen, Stellvertreter-Duell, Staffel - ohne Teamspiel meldet sie der Spielstart.</summary>
    public static partial class MinigameProfiles
    {
        // Die Dauer liest das Tauziehen wie der Tippsprint; gezogen wird erst nach dem Countdown, gezählt an der Serveruhr.
        private static readonly MinigameProfile TugOfWar = new()
        {
            Kind = MinigameKind.TugOfWar,
            TeamScoring = MinispielTeamwertung.NurImTeamspiel,
            DisplayName = "Tauziehen",
            Description = "Nur im Teamspiel. Nach dem Countdown tippen alle so oft wie möglich auf ihr Telefon – jeder Tipp "
                        + "zieht das Seil zum eigenen Team, am Beamer wandert es mit. Es zieht die Summe oder der Durchschnitt "
                        + "des Teams, wie am Spiel eingestellt; wer beim Start nicht verbunden ist, zieht nicht mit. Wenn die Zeit "
                        + "an der Uhr des Spielleiters um ist, bekommt das stärkere Team den Vorschlag mit den Punkten des Felds, "
                        + "aufgeteilt auf seine Mitglieder, bei Gleichstand alle vorn. Mehr als "
                        + $"{TauziehenEvaluator.MaxTapsPerSecond} Tipps je Sekunde nimmt der Server nicht an. Nie Minus.",
            Variants =
            [
                new("Stärkstes Team gewinnt",
                    $"Tippt {DurationPlaceholder} Sekunden lang so oft wie möglich – jeder Tipp zieht das Seil zu eurem Team. "
                    + "Wer am Ende stärker zieht, gewinnt."),
            ],
            TeamRulesText = $"Im Teamspiel zieht {TeamValuePlaceholder} eures Teams; die Punkte werden aufgeteilt.",
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 15,
            MinDurationSeconds = 5,
            MaxDurationSeconds = 60,
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Zwei Teams zu je einem - die Teams prüft der Spielstart eigens (MinigameChecks.ForTeams).
            MinPlayers = 2,
        };

        // M37 T10: die Disziplin spielt wie das Duell (DuellEvaluator) - Reaktion mit jeder Zahl von Teams, Schere-Stein-Papier
        // nur bei genau zwei (StellvertreterEvaluator.Disziplin). Der Index ist MinigameQuestion.Variant.
        private static readonly MinigameProfile ProxyDuel = new()
        {
            Kind = MinigameKind.ProxyDuel,
            TeamScoring = MinispielTeamwertung.NurImTeamspiel,
            DisplayName = "Stellvertreter-Duell",
            Description = "Nur im Teamspiel. Jedes Team wählt am Telefon einen Stellvertreter aus seinen Reihen oder lässt "
                        + "losen – die erste Wahl gilt fürs Team, gelost wird beim Spielleiter. Dann spielen nur die Gewählten, "
                        + "die anderen schauen zu: Reaktion, bei genau zwei Teams wahlweise Schere, Stein, Papier. Das Team des "
                        + "Siegers bekommt den Vorschlag mit den Punkten des Felds, aufgeteilt auf seine Mitglieder; bei gleicher "
                        + "Bestzeit alle vorn, bei einem Unentschieden niemand. Wer beim Start nicht verbunden ist, spielt nicht "
                        + "mit. Wählt ein Team nicht, lost der Spielleiter im Fragefenster. Nie Minus.",
            Variants =
            [
                new("Reaktion",
                    "Jedes Team schickt einen Stellvertreter – wählt ihn am Telefon oder lasst losen. Die Stellvertreter "
                    + $"warten auf Grün und „JETZT!“; die beste Zeit aus {RoundsPlaceholder} Runden gewinnt für ihr Team."),
                new("Schere, Stein, Papier (nur zwei Teams)",
                    "Jedes Team schickt einen Stellvertreter – wählt ihn am Telefon oder lasst losen. Die beiden spielen "
                    + $"Schere, Stein, Papier, Best of {RoundsPlaceholder}; wer gewinnt, gewinnt für sein Team."),
            ],
            TeamRulesText = "Im Teamspiel holt der Sieger die Punkte für sein Team, aufgeteilt auf alle.",
            Layout = BuzzerControlsLayout.Minigame,
            ShowRounds = true,
            DefaultRounds = 3,
            MinOptions = 0,
            OptionsName = "Zeilen",
            MinPlayers = 2,
            TwoTeamsOnlyVariant = StellvertreterEvaluator.SchereSteinPapierVariant,
        };

        // M37 T11: die Teile sind die Zeilen in ihrer Reihenfolge; geprüft wird am Server (StaffelEvaluator), kein Telefon sieht
        // eine Lösung vor der Auflösung. Läuft der Inhalt über MinispielInhalt, zählt eine leere Zeile nicht (D6).
        private static readonly MinigameProfile Relay = new()
        {
            Kind = MinigameKind.Relay,
            TeamScoring = MinispielTeamwertung.NurImTeamspiel,
            DisplayName = "Staffel",
            Description = "Nur im Teamspiel. Die Zeilen sind die Teile einer Folge in ihrer Reihenfolge – etwa die Planeten von innen "
                        + "nach außen. Ein Team läuft los: Seine Mitglieder tippen der Reihe nach je den nächsten Teil am Telefon, die "
                        + "anderen sehen, wer dran ist. Jede Antwort prüft der Server, groß und klein zählt nicht, ä gilt wie ae. "
                        + "Ein Fehler gibt den Teil ans nächste Team weiter; haben ihn alle verfehlt, geht es mit dem nächsten Teil "
                        + "weiter. Im Fragefenster kann der Spielleiter eine abgelehnte Antwort gelten lassen oder für das Team am Zug "
                        + "weitergeben. Das Team mit den meisten Teilen bekommt den Vorschlag mit den Punkten des Felds, aufgeteilt auf "
                        + "seine Mitglieder, bei Gleichstand alle vorn. Wer beim Start nicht verbunden ist, läuft nicht mit. Mehr als "
                        + $"{StaffelEvaluator.MaxAntwortenJeFenster} Antworten in {StaffelEvaluator.FensterSekunden} Sekunden nimmt der "
                        + "Server von einem Telefon nicht an. Nie Minus.",
            Variants =
            [
                new("Der Reihe nach",
                    "Staffel! Wer dran ist, tippt den nächsten Teil – danach ist das nächste Mitglied eures Teams dran. Ein Fehler "
                    + "gibt ans nächste Team weiter; die meisten Teile gewinnen."),
            ],
            TeamRulesText = "Im Teamspiel holt das Team mit den meisten Teilen die Punkte, aufgeteilt auf alle.",
            Layout = BuzzerControlsLayout.Minigame,
            MinOptions = StaffelEvaluator.MinTeile,
            OptionsName = "Teile",
            CountsAsOption = t => !string.IsNullOrWhiteSpace(t),
            MinPlayers = 2,
        };
    }
}
