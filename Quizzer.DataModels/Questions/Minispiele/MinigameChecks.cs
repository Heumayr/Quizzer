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
        public const string NotInTeamGame = "minigame-not-in-team-game";
        public const string NoOpponent = "minigame-no-opponent";
        public const string OnlyInTeamGame = "minigame-only-in-team-game";

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

            var mindestens = art.OptionPerRound ? Math.Max(art.MinOptions, frage.Rounds) : art.MinOptions;

            if (zeilen < mindestens)
                yield return new(TooFewOptions, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: mindestens {mindestens} {art.OptionsName} nötig, "
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

        /// <summary>
        /// Prueft, ob das Minispiel zum Spiel passt (M37 T8): im Teamspiel nach Teams, ohne Teamspiel nur die Teamarten (T9). Eine
        /// unbekannte Art meldet schon <see cref="Content"/>.
        /// </summary>
        public static IEnumerable<ValidationIssue> ForTeams(MinigameQuestion frage, Models.Base.Game spiel)
        {
            ArgumentNullException.ThrowIfNull(frage);
            ArgumentNullException.ThrowIfNull(spiel);

            if (!MinigameProfiles.TryFor(frage.Kind, out var art))
                return [];

            if (!spiel.IsTeamGame)
                return OhneTeamspiel(art);

            // Eine Teamart zählt nur besetzte Teams - wer ohne Team ist, spielt dort nicht mit (T9); sonst zählt er als eigenes Lager.
            return ForTeams(art, art.TeamScoring == MinispielTeamwertung.NurImTeamspiel
                ? Teams.Teamzuordnung.BesetzteTeams(spiel)
                : Teams.Teamzuordnung.Lager(spiel));
        }

        /// <summary>
        /// Der Riegel „nicht im Teamspiel“ (M37 T8), fail-closed: eine Art, die es nicht spielt - oder deren Wertung unbekannt
        /// ist -, ist ein Fehler. Eine Art je Person braucht einen Gegner, eine Teamart (T9) ein zweites Team - beide also
        /// mindestens zwei <paramref name="lager"/>.
        /// </summary>
        public static IEnumerable<ValidationIssue> ForTeams(MinigameProfile art, int lager)
        {
            ArgumentNullException.ThrowIfNull(art);

            if (art.TeamScoring == MinispielTeamwertung.Zusammengefasst
                || art.TeamScoring is MinispielTeamwertung.JePerson or MinispielTeamwertung.NurImTeamspiel && lager >= 2)
                yield break;

            var besetzt = lager == 1 ? "ist eins" : "ist keins";

            yield return art.TeamScoring switch
            {
                MinispielTeamwertung.JePerson => new(NoOpponent, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: im Teamspiel nur gegen Gegner – dafür braucht es Mitspieler in mindestens "
                    + $"zwei Teams, besetzt {besetzt}."),
                MinispielTeamwertung.NurImTeamspiel => new(NoOpponent, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName}: Team gegen Team – dafür braucht es Mitspieler in mindestens zwei Teams, "
                    + $"besetzt {besetzt}."),
                _ => new(NotInTeamGame, ValidationSeverity.Error,
                    $"Minispiel {art.DisplayName} wird im Teamspiel nicht gespielt – im Spielaufbau ein anderes Minispiel "
                    + "auf die Zelle legen."),
            };
        }

        /// <summary>Der Riegel „nur im Teamspiel“ (M37 T9): ohne Teamspiel hätte bei einer Teamart niemand ein Team, das spielt.</summary>
        public static IEnumerable<ValidationIssue> OhneTeamspiel(MinigameProfile art)
        {
            ArgumentNullException.ThrowIfNull(art);

            if (art.TeamScoring != MinispielTeamwertung.NurImTeamspiel)
                yield break;

            yield return new(OnlyInTeamGame, ValidationSeverity.Error,
                $"Minispiel {art.DisplayName} gibt es nur im Teamspiel – im Spielaufbau „Teamspiel“ einschalten oder ein "
                + "anderes Minispiel auf die Zelle legen.");
        }
    }
}
