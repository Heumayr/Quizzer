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

        /// <summary>Alle Profile in der Reihenfolge, in der sie angeboten werden.</summary>
        public static IReadOnlyList<MinigameProfile> All { get; } = [Majority];

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
                : string.Empty;
        }
    }
}
