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
    }
}
