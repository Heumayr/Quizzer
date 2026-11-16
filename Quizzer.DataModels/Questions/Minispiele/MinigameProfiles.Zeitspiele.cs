using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Zeitspiele im Telefonrahmen (M35 C1): Tippsprint und Stoppuhr.</summary>
    public static partial class MinigameProfiles
    {
        /// <summary>Die laengste Zielzeit der Stoppuhr - auch die Obergrenze ihres Bestwerts (M35 C9).</summary>
        public const int StopwatchMaxSeconds = 60;

        private static readonly MinigameProfile TapSprint = new()
        {
            Kind = MinigameKind.TapSprint,
            DisplayName = "Tippsprint",
            Description = "Nach dem Countdown tippen alle so oft wie möglich auf ihr Telefon, bis die Zeit "
                        + "um ist; am Beamer läuft das Rennen mit. Wer am häufigsten getippt hat, bekommt "
                        + "den Vorschlag „richtig“ mit den Punkten des Felds, bei Gleichstand alle. Mehr als "
                        + $"{TippsprintEvaluator.MaxTapsPerSecond} Tipps je Sekunde nimmt der Server nicht an. "
                        + "Wer nichts abgibt, bekommt keinen Vorschlag.",
            Variants =
            [
                new("Meiste Tipps gewinnt",
                    $"Tippt {DurationPlaceholder} Sekunden lang so oft wie möglich auf euer Telefon. Wer am "
                    + "häufigsten tippt, gewinnt."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 10,
            MinDurationSeconds = 3,
            MaxDurationSeconds = 60,
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Wie die Reaktion: allein gewaenne man immer.
            MinPlayers = 2,
            Record = MinigameRecords.Tippsprint,
        };

        // Die Zielzeit steckt in DurationSeconds: ein Parameter, je Art gedeutet - keine neue Spalte (M35 C1).
        private static readonly MinigameProfile Stopwatch = new()
        {
            Kind = MinigameKind.Stopwatch,
            DisplayName = "Stoppuhr",
            Description = "Jeder startet am Telefon seine Uhr. Sie ist drei Sekunden lang zu sehen und "
                        + "verschwindet dann; gestoppt wird mit einem Tipp bei der Zielzeit. Wer am nächsten "
                        + "an der Zielzeit liegt, bekommt den Vorschlag „richtig“ mit den Punkten des Felds, "
                        + "bei Gleichstand alle. Wer nicht stoppt oder mehr als doppelt so lange braucht, "
                        + "bekommt keinen Vorschlag.",
            Variants =
            [
                new("Nächste an der Zielzeit gewinnt",
                    $"Startet eure Uhr und stoppt sie nach genau {DurationPlaceholder} Sekunden – nach drei "
                    + "Sekunden verschwindet sie. Wer am nächsten dran ist, gewinnt."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DurationLabel = "Zielzeit (Sekunden)",
            DurationToolTip = "Bei dieser Zeit soll gestoppt werden. Zu sehen ist die Uhr nur die ersten drei Sekunden.",
            DefaultDurationSeconds = 10,
            MinDurationSeconds = 5,
            MaxDurationSeconds = StopwatchMaxSeconds,
            MinOptions = 0,
            OptionsName = "Zeilen",
            MinPlayers = 2,
            Record = MinigameRecords.Stoppuhr,
        };
    }
}
