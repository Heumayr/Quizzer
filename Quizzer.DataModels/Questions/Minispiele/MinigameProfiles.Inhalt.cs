using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Inhaltsspiele (M35 C5): Kopfrechnen, Memory, Blitzrunde, Buchstabensalat.</summary>
    public static partial class MinigameProfiles
    {
        private static readonly MinigameProfile MentalArithmetic = new()
        {
            Kind = MinigameKind.MentalArithmetic,
            DisplayName = "Kopfrechnen",
            Description = "Alle bekommen am Telefon dieselbe Folge von Rechenaufgaben und tippen die Ergebnisse ein. "
                        + "Jede Antwort prüft der Server, danach kommt sofort die nächste Aufgabe – eine falsche kostet "
                        + "nur Zeit. Wer in der Zeit die meisten richtig hat, bekommt den Vorschlag mit den Punkten des "
                        + "Felds, bei Gleichstand alle. Mehr als "
                        + $"{KopfrechnenEvaluator.MaxAntwortenJeSekunde} Antworten je Sekunde nimmt der Server nicht an.",

            // Der Index ist MinigameQuestion.Variant - KopfrechnenAufgaben.Leicht/Mittel/Schwer.
            Variants =
            [
                new("Leicht – Plus und Minus bis 20",
                    $"Kopfrechnen! {DurationPlaceholder} Sekunden Plus und Minus bis 20 – wer die meisten richtig hat, gewinnt."),
                new("Mittel – bis 100 und kleines Einmaleins",
                    $"Kopfrechnen! {DurationPlaceholder} Sekunden Plus und Minus bis 100 und das kleine Einmaleins – wer die "
                    + "meisten richtig hat, gewinnt."),
                new("Schwer – bis 1000, mal und geteilt",
                    $"Kopfrechnen! {DurationPlaceholder} Sekunden Plus und Minus bis 1000, Mal und Geteilt – wer die meisten "
                    + "richtig hat, gewinnt."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 60,
            MinDurationSeconds = 15,
            MaxDurationSeconds = 300,
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Wie der Tippsprint: allein gewaenne man immer.
            MinPlayers = 2,
            Record = MinigameRecords.Kopfrechnen,
        };

        private const string MemoryRegeltext =
            $"Memory! Findet auf eurem Telefon alle Paare – wer zuerst fertig ist, gewinnt. Höchstens {DurationPlaceholder} Sekunden.";

        private static readonly MinigameProfile Memory = new()
        {
            Kind = MinigameKind.Memory,
            DisplayName = "Memory",
            Description = "Jeder spielt auf seinem Telefon ein eigenes Memory mit derselben Belegung. Jede umgedrehte Karte "
                        + "fragt den Server – die Belegung bleibt dort, das Telefon kennt nur die offenen Karten und seine "
                        + "gefundenen Paare. Zwei Karten, die kein Paar sind, bleiben offen, bis die nächste umgedreht wird. "
                        + "Wer zuerst alle Paare hat (gemessen an der Uhr des Servers), bekommt den Vorschlag mit den Punkten "
                        + "des Felds; läuft die Zeit vorher ab, wer die meisten Paare hat. Bei Gleichstand alle. Mehr als "
                        + $"{MemoryEvaluator.MaxUmdrehenJeSekunde} Karten je Sekunde nimmt der Server nicht an.",

            // Der Index ist MinigameQuestion.Variant - MemoryBelegung.PaareFuer.
            Variants =
            [
                new("Klein – 6 Paare", MemoryRegeltext),
                new("Mittel – 8 Paare", MemoryRegeltext),
                new("Groß – 10 Paare", MemoryRegeltext),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 120,
            MinDurationSeconds = 20,
            MaxDurationSeconds = 300,
            DurationLabel = "Höchstdauer (Sekunden)",
            DurationToolTip = "So lange läuft die Runde höchstens – sie endet früher, wenn alle fertig sind.",
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Wie der Tippsprint: allein gewaenne man immer.
            MinPlayers = 2,
        };

        private static readonly MinigameProfile LightningRound = new()
        {
            Kind = MinigameKind.LightningRound,
            DisplayName = "Blitzrunde",
            Description = "Alle beurteilen am Telefon dieselben Aussagen, eine nach der anderen: wahr oder falsch? Die nächste "
                        + "kommt erst nach der Antwort; die Reihenfolge wird je Runde gemischt und ist für alle gleich. Wahr ist "
                        + "eine Aussage mit Haken. Richtig zählt +1, falsch −1, keine Antwort 0; der Vorschlag ist dieser Anteil "
                        + "der Punkte des Felds – alle richtig bringt sie ganz, mehr falsche als richtige ein Minus. Ob eine Antwort "
                        + "stimmte, erfährt das Telefon erst nach der Runde. Mehr als "
                        + $"{BlitzrundeEvaluator.MaxAntwortenJeSekunde} Antworten je Sekunde nimmt der Server nicht an.",
            Variants =
            [
                new("Wahr oder falsch",
                    $"Blitzrunde! Wahr oder falsch? Richtig +1, falsch −1 – ihr habt {DurationPlaceholder} Sekunden."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 60,
            MinDurationSeconds = 15,
            MaxDurationSeconds = 300,

            // Mit weniger als vier Aussagen entscheidet ein einziger Muenzwurf ueber die halben Feldpunkte (Annahme C5b).
            MinOptions = 4,
            OptionsName = "Aussagen",
            ShowTruePerRow = true,

            // Wie MinispielInhalt.Zeilen: eine Zeile aus Leerzeichen spielt das Spiel nicht, also zaehlt sie nicht (V13).
            CountsAsOption = t => !string.IsNullOrWhiteSpace(t),
        };

        private static readonly MinigameProfile WordScramble = new()
        {
            Kind = MinigameKind.WordScramble,
            DisplayName = "Buchstabensalat",
            Description = "Alle sehen am Telefon denselben Begriff mit verwürfelten Buchstaben und tippen die Lösung. Wer ihn "
                        + "zuerst richtig hat (gemessen an der Uhr des Servers), löst ihn, und für alle kommt der nächste. Groß "
                        + "und klein zählt nicht, ä gilt wie ae, ö wie oe, ü wie ue, ß wie ss. Der Vorschlag ist der Anteil der "
                        + "Punkte des Felds je gelöstem Begriff – wer alle löst, bekommt sie ganz; falsch raten kostet nichts. "
                        + $"Mehr als {BuchstabensalatEvaluator.MaxVersucheJeFenster} Versuche in "
                        + $"{BuchstabensalatEvaluator.FensterSekunden} Sekunden nimmt der Server nicht an.",
            Variants =
            [
                new("Verwürfelte Begriffe",
                    $"Buchstabensalat! Wer den Begriff zuerst entwirrt, bekommt ihn – ihr habt {DurationPlaceholder} Sekunden."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowDurationSeconds = true,
            DefaultDurationSeconds = 90,
            MinDurationSeconds = 20,
            MaxDurationSeconds = 300,
            DurationLabel = "Höchstdauer (Sekunden)",
            DurationToolTip = "So lange läuft die Runde höchstens – sie endet früher, wenn alle Begriffe gelöst sind.",

            // Ein oder zwei Begriffe sind ein Wettlauf um ein Wort, kein Spiel (Annahme C5b); unverwuerfelbare zaehlen nicht.
            MinOptions = 3,
            OptionsName = "Begriffe",
            CountsAsOption = BuchstabensalatBegriffe.Verwuerfelbar,
        };
    }
}
