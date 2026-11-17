using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die mehrphasigen Spiele (M35 C6): Erklärbär, Bluff und Heiße Kartoffel.</summary>
    public static partial class MinigameProfiles
    {
        private static readonly MinigameProfile ExplainIt = new()
        {
            Kind = MinigameKind.ExplainIt,
            TeamScoring = MinispielTeamwertung.Zusammengefasst,
            DisplayName = "Erklärbär",
            Description = "Je Runde ein Begriff aus den Zeilen: nur das Telefon des Erklärers zeigt ihn, er beschreibt ihn laut, "
                        + "ohne ihn zu sagen. Die anderen tippen ihre Rateversuche ins Telefon; wer zuerst richtig liegt "
                        + "(gemessen an der Uhr des Servers), und der Erklärer bekommen je die Punkte des Felds geteilt durch die "
                        + "Runden. Groß und klein zählt nicht, ä gilt wie ae, ö wie oe, ü wie ue, ß wie ss. Sagt der Erklärer das "
                        + "Wort, wertet der Spielleiter „Verraten“ – dann bringt die Runde niemandem etwas. Erklärer ist, wer das "
                        + "Feld aufgedeckt hat; fehlt er, wählt ihn der Spielleiter. Mehr als "
                        + $"{ErklaerbaerEvaluator.MaxVersucheJeFenster} Versuche in {ErklaerbaerEvaluator.FensterSekunden} Sekunden "
                        + "nimmt der Server nicht an.",
            Variants =
            [
                new("Begriffe erklären",
                    $"Erklärbär! Einer erklärt, alle anderen raten am Telefon – {RoundsPlaceholder} Begriffe, je "
                    + $"{DurationPlaceholder} Sekunden. Das Wort selbst ist verboten!"),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowRounds = true,
            DefaultRounds = 3,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 60,
            MinDurationSeconds = 15,
            MaxDurationSeconds = 300,
            DurationLabel = "Höchstdauer je Begriff (Sekunden)",
            DurationToolTip = "So lange darf je Begriff geraten werden – die Runde endet früher, sobald jemand richtig liegt.",
            MinOptions = 1,
            OptionsName = "Begriffe",
            OptionPerRound = true,
            CountsAsOption = t => t.Any(char.IsLetterOrDigit),

            // Einer erklaert, mindestens einer raet.
            MinPlayers = 2,
            ChooserOnly = true,
            ChooserLabel = "Wer erklärt",
        };

        private static readonly MinigameProfile Bluff = new()
        {
            Kind = MinigameKind.Bluff,
            TeamScoring = MinispielTeamwertung.JePerson,
            DisplayName = "Bluff",
            Description = "Zwei Phasen. Zuerst tippt jeder am Telefon eine falsche Antwort auf die Frage, die echt klingt. Dann "
                        + "wählt jeder unter allen Bluffs und der echten Antwort, gemischt – den eigenen Bluff sieht er nicht. Wer "
                        + "die echte findet, bekommt die Punkte des Felds; für jeden, der auf den eigenen Bluff hereinfällt, die "
                        + "Hälfte. Die echte Antwort ist die erste Zeile, weitere Zeilen gelten als gleichwertige Schreibweisen; "
                        + "ein Bluff, der einer davon gleicht, ist von selbst gestrichen. Zwischen den Phasen streicht der "
                        + "Spielleiter weitere, die der echten zu nah sind. Gleiche Bluffs stehen einmal da und zählen für jeden, "
                        + "der sie schrieb. Wer keinen Bluff schreibt, wählt trotzdem.",
            Variants =
            [
                new("Bluffen und entlarven",
                    "Bluff! Tippt eine falsche Antwort, die echt klingt. Dann wählt jeder die echte – wer sie findet, bekommt "
                    + "die Punkte; für jeden, der auf deinen Bluff hereinfällt, gibt es die Hälfte."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            FixedRounds = 2,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 60,
            MinDurationSeconds = 20,
            MaxDurationSeconds = 300,
            DurationLabel = "Höchstdauer je Phase (Sekunden)",
            DurationToolTip = "So lange dauert jede der beiden Phasen höchstens – sie endet früher, wenn alle abgegeben haben.",
            MinOptions = 1,
            OptionsName = "Antworten",
            CountsAsOption = t => t.Any(char.IsLetterOrDigit),

            // Mit zweien gaebe es nur einen fremden Bluff - die Wahl waere ein Muenzwurf (Annahme C6a).
            MinPlayers = 3,
            TeamRulesText = "Im Teamspiel zählen nur Gegner, die auf deinen Bluff hereinfallen.",
        };

        private static readonly MinigameProfile HotPotato = new()
        {
            Kind = MinigameKind.HotPotato,
            TeamScoring = MinispielTeamwertung.JePerson,
            DisplayName = "Heiße Kartoffel",
            Description = "Wer die Kartoffel hält, beantwortet am Telefon Aussagen mit wahr oder falsch – wahr ist eine Aussage mit "
                        + "Haken. Nur nach einer richtigen Antwort gibt er sie an einen Mitspieler seiner Wahl weiter, nach einer "
                        + "falschen kommt die nächste Aussage. Direkt zurück an den, der sie gerade gegeben hat, geht nur, wenn sonst "
                        + "niemand verbunden ist. Irgendwann platzt sie: Der Zünder ist geheim, auch der Spielleiter kennt ihn nicht; "
                        + "er geht zwischen einem Drittel und der ganzen eingestellten Zeit hoch. Wer sie dann hält, verliert den "
                        + "Anteil der Feldpunkte (gekappt, wenn das Spiel kein Minus erlaubt), wer sie ihm zugeschoben hat, gewinnt "
                        + "ihn. Zuerst hält sie, wer das Feld aufgedeckt hat; fehlt er, wählt der Spielleiter. Mehr als "
                        + $"{KartoffelEvaluator.MaxAntwortenJeFenster} Antworten in {KartoffelEvaluator.FensterSekunden} Sekunden "
                        + "nimmt der Server nicht an.",
            Variants =
            [
                new("Wahr oder falsch",
                    "Heiße Kartoffel! Wer sie hält, beantwortet Aussagen – nur nach einer richtigen Antwort darf er sie "
                    + $"weitergeben. Wann sie platzt, weiß niemand: Wer sie dann hält, verliert {SharePlaceholder} % der "
                    + "Feldpunkte, wer sie ihm zugeschoben hat, gewinnt sie."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowShare = true,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 60,
            MinDurationSeconds = 20,
            MaxDurationSeconds = 300,
            DurationLabel = "Längste Zündschnur (Sekunden)",
            DurationToolTip = "Der Zünder geht zufällig zwischen einem Drittel dieser Zeit und ihr ganz hoch – wann genau, weiß "
                            + "niemand, auch der Spielleiter nicht.",

            // Die Aussagen kommen reihum wieder; mit weniger als sechs kennt man sie nach einem Durchgang (Annahme C6b).
            MinOptions = 6,
            OptionsName = "Aussagen",
            ShowTruePerRow = true,
            CountsAsOption = t => t.Any(char.IsLetterOrDigit),

            // Mit zweien geht sie hin und her - direkt zurueck ist dann erlaubt, sonst steckte sie fest.
            MinPlayers = 2,
            ChooserOnly = true,
            ChooserLabel = "Wer hält zuerst",
            TeamRulesText = "Im Teamspiel geht die Kartoffel nur an Gegner aus anderen Teams.",
        };
    }
}
