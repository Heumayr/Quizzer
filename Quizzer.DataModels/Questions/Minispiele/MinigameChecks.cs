using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Was ein Minispiel braucht, je Art aus dem Profil gelesen (M35 B2, V2).
    /// <para>
    /// <b>Zwei Teile:</b> der Inhalt gehoert zur Frage und wird schon beim Speichern geprueft
    /// (<see cref="QuestionValidator"/>); die Zahl der Mitspieler kennt erst der Spielstart. Eine
    /// neue Art traegt ihre Mindestwerte nur in ihr Profil ein.
    /// </para>
    /// </summary>
    public static class MinigameChecks
    {
        public const string KindUnknown = "minigame-kind-unknown";
        public const string VariantUnknown = "minigame-variant-unknown";
        public const string TooFewOptions = "minigame-too-few-options";
        public const string TooFewPlayers = "minigame-too-few-players";
        public const string RoundsOutOfRange = "minigame-rounds-out-of-range";
        public const string DurationOutOfRange = "minigame-duration-out-of-range";
        public const string ShareOutOfRange = "minigame-share-out-of-range";

        /// <summary>Prueft den Inhalt: bekannte Art, bekannte Spielweise, genug Zeilen.</summary>
        public static IEnumerable<ValidationIssue> Content(MinigameQuestion frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            if (!MinigameProfiles.TryFor(frage.Kind, out var art))
            {
                yield return new(KindUnknown, ValidationSeverity.Error,
                    $"Das Minispiel hat eine unbekannte Art ({(int)frage.Kind}) - vermutlich aus einer "
                    + "neueren Fassung. In der Maske eine Art wählen.", nameof(frage.Kind));
                yield break;
            }

            if (frage.Variant < 0 || frage.Variant >= art.Variants.Count)
                yield return new(VariantUnknown, ValidationSeverity.Warning,
                    $"Minispiel {art.DisplayName}: die gespeicherte Spielweise gibt es nicht, gespielt "
                    + $"wird „{art.Variants[0].Name}“.", nameof(frage.Variant));

            if (art.ShowRounds && (frage.Rounds < 1 || frage.Rounds > MinigameProfile.MaxRounds))
                yield return new(RoundsOutOfRange, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: 1 bis {MinigameProfile.MaxRounds} Runden, eingetragen "
                    + $"sind {frage.Rounds}.", nameof(frage.Rounds));

            if (art.ShowDurationSeconds
                && (frage.DurationSeconds < art.MinDurationSeconds || frage.DurationSeconds > art.MaxDurationSeconds))
                yield return new(DurationOutOfRange, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: {art.DurationLabel} {art.MinDurationSeconds} bis "
                    + $"{art.MaxDurationSeconds}, eingetragen sind {frage.DurationSeconds}.", nameof(frage.DurationSeconds));

            // Ein Anteil von 0 bewegt nichts - das Spiel liefe ins Leere (M35 C2).
            if (art.ShowShare && (frage.Share < 1 || frage.Share > 100))
                yield return new(ShareOutOfRange, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: Anteil 1 bis 100 Prozent, eingetragen sind {frage.Share}.",
                    nameof(frage.Share));

            var zeilen = (frage.Steps ?? []).Count(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly
                && art.CountsAsOption((string.IsNullOrWhiteSpace(s.StepText) ? s.Designation : s.StepText) ?? string.Empty));

            if (zeilen < art.MinOptions)
                yield return new(TooFewOptions, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: mindestens {art.MinOptions} {art.OptionsName} nötig, "
                    + $"vorhanden sind {zeilen}.", nameof(frage.Steps));
        }

        /// <summary>Prueft, ob das Spiel genug Mitspieler hat - nur beim Spielstart bekannt.</summary>
        public static IEnumerable<ValidationIssue> ForPlayers(MinigameQuestion frage, int spielerzahl)
        {
            ArgumentNullException.ThrowIfNull(frage);

            if (!MinigameProfiles.TryFor(frage.Kind, out var art) || spielerzahl >= art.MinPlayers)
                yield break;

            yield return new(TooFewPlayers, ValidationSeverity.Error,
                $"Minispiel {art.DisplayName}: braucht mindestens {art.MinPlayers} Mitspieler, im "
                + $"Spiel sind {spielerzahl}.");
        }
    }
}
