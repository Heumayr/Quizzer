using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System.Diagnostics.CodeAnalysis;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Minispiel-Profile. Einzige Stelle, an der steht, was eine Art mitbringt (M35 B2).
    /// </summary>
    public static class MinigameProfiles
    {
        private static readonly MinigameProfile Majority = new()
        {
            Kind = MinigameKind.Majority,
            DisplayName = "Mehrheit",
            Description = "Jeder wählt am Telefon geheim eine der Optionen. Wer zur größten Gruppe "
                        + "gehört, bekommt den Vorschlag „richtig“ mit den Punkten des Felds; bei "
                        + "Gleichstand alle größten Gruppen. Wer nichts wählt, bekommt keinen Vorschlag.",

            // Der Index ist MinigameQuestion.Variant - MehrheitEvaluator.MinorityVariant zeigt auf den zweiten.
            Variants =
            [
                new("Mehrheit gewinnt",
                    "Wählt geheim eine Antwort. Wer zur größten Gruppe gehört, bekommt die Punkte."),
                new("Minderheit gewinnt",
                    "Wählt geheim eine Antwort. Wer zur kleinsten Gruppe gehört, bekommt die Punkte."),
            ],
            MinOptions = 2,

            // Allein ist man immer die Mehrheit - die Punkte waeren geschenkt (Annahme, siehe Bericht B2).
            MinPlayers = 2,
        };

        private static readonly MinigameProfile Reaction = new()
        {
            Kind = MinigameKind.Reaction,
            DisplayName = "Reaktion",
            Description = "Alle Telefone zeigen Rot und schalten nach einer zufälligen Wartezeit auf Grün "
                        + "mit „JETZT!“. Wer zu früh tippt, verliert die Runde. Über alle Runden zählt "
                        + "die beste Einzelzeit: sie bekommt den Vorschlag „richtig“ mit den Punkten des "
                        + "Felds, bei Gleichstand alle. Wer nichts abgibt, bekommt keinen Vorschlag.",
            Variants =
            [
                new("Beste Zeit gewinnt",
                    "Wartet auf Grün und „JETZT!“ – dann so schnell wie möglich tippen. Wer zu früh "
                    + "tippt, verliert die Runde. Die beste Zeit gewinnt."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowRounds = true,
            DefaultRounds = 3,
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Wie die Mehrheit: allein gewaenne man immer.
            MinPlayers = 2,
        };

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
            MaxDurationSeconds = 60,
            MinOptions = 0,
            OptionsName = "Zeilen",
            MinPlayers = 2,
        };

        /// <summary>Steht im Regeltext fuer <see cref="MinigameQuestion.DurationSeconds"/> - <see cref="RulesFor"/> setzt die Zahl ein.</summary>
        public const string DurationPlaceholder = "{Dauer}";

        /// <summary>Alle Profile in der Reihenfolge, in der sie angeboten werden.</summary>
        public static IReadOnlyList<MinigameProfile> All { get; } = [Majority, Reaction, TapSprint, Stopwatch];

        /// <summary>Liefert das Profil zu einer Art.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Bei einer unbekannten Art.</exception>
        public static MinigameProfile For(MinigameKind kind)
            => TryFor(kind, out var profil)
                ? profil
                : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unbekannte Minispiel-Art.");

        /// <summary>Wie <see cref="For"/>, aber ohne Ausnahme - fuer Zeilen aus einer neueren Fassung.</summary>
        public static bool TryFor(MinigameKind kind, [NotNullWhen(true)] out MinigameProfile? profil)
        {
            profil = All.FirstOrDefault(p => p.Kind == kind);
            return profil != null;
        }

        /// <summary>Der Regeltext, wie er am Beamer steht - leer bei einer unbekannten Art.</summary>
        public static string RulesFor(MinigameQuestion frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            return TryFor(frage.Kind, out var profil)
                ? profil.VariantFor(frage.Variant).RulesText.Replace(DurationPlaceholder, frage.DurationSeconds.ToString())
                : string.Empty;
        }
    }
}
