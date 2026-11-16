using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System.Diagnostics.CodeAnalysis;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Minispiel-Profile. Einzige Stelle, an der steht, was eine Art mitbringt (M35 B2). Die Arten stehen nach Thema in
    /// Teildateien (<c>MinigameProfiles.*.cs</c>); hier Mehrheit und Reaktion, die Platzhalter und der Zugriff.
    /// </summary>
    public static partial class MinigameProfiles
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

        /// <summary>Steht im Regeltext fuer <see cref="MinigameQuestion.DurationSeconds"/> - <see cref="RulesFor"/> setzt die Zahl ein.</summary>
        public const string DurationPlaceholder = "{Dauer}";

        /// <summary>Ebenso fuer <see cref="MinigameQuestion.Rounds"/> (M35 C2).</summary>
        public const string RoundsPlaceholder = "{Runden}";

        /// <summary>Ebenso fuer <see cref="MinigameQuestion.Share"/> (M35 C2).</summary>
        public const string SharePlaceholder = "{Anteil}";

        /// <summary>
        /// Alle Profile in der Reihenfolge, in der sie angeboten werden. Erst beim ersten Zugriff gebaut: die Profile stehen in
        /// Teildateien, und in welcher Reihenfolge C# deren Feldinitialisierer ausfuehrt, ist nicht festgelegt.
        /// </summary>
        public static IReadOnlyList<MinigameProfile> All => alle ??=
            [Majority, Reaction, TapSprint, Stopwatch, Duel, RobberAndGuard, ShareOrTake, Crash, WheelOfFortune, MentalArithmetic, Memory,
             LightningRound, WordScramble, ExplainIt, Bluff];

        private static IReadOnlyList<MinigameProfile>? alle;

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
                ? profil.VariantFor(frage.Variant).RulesText
                    .Replace(DurationPlaceholder, frage.DurationSeconds.ToString())
                    .Replace(RoundsPlaceholder, frage.Rounds.ToString())
                    .Replace(SharePlaceholder, frage.Share.ToString())
                : string.Empty;
        }
    }
}
