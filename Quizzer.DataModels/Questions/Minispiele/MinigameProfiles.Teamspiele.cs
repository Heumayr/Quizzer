using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Arten nur im Teamspiel (M37 T9 ff.): Team gegen Team, ohne Teamspiel meldet sie der Spielstart.</summary>
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
    }
}
