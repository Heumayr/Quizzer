using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Spielweise einer Art: der Name im Editor und der Regeltext fuer den Beamer.</summary>
    /// <param name="Name">Wie die Spielweise im Editor heisst.</param>
    /// <param name="RulesText">Was am Beamer ueber den Optionen steht.</param>
    public sealed record MinigameVariant(string Name, string RulesText);

    /// <summary>
    /// Beschreibt eine Minispiel-Art: wie sie heisst, wie sie gespielt wird, welche der allgemeinen
    /// Parameter sie liest und was sie mindestens braucht (M35 B2).
    /// <para>
    /// Gebaut wie <see cref="QuestionTypeProfile"/> und aus demselben Grund: Maske, Beamer, Pruefung
    /// und Spielstart lesen von hier, statt dasselbe Wissen viermal zu fuehren.
    /// </para>
    /// </summary>
    public sealed class MinigameProfile
    {
        /// <summary>Die Art, die dieses Profil beschreibt.</summary>
        public required MinigameKind Kind { get; init; }

        /// <summary>Name der Art, wie Spielleiter und Beamer ihn sehen („Mehrheit").</summary>
        public required string DisplayName { get; init; }

        /// <summary>Fuer den Spielleiter im Editor: wie gespielt und gewertet wird.</summary>
        public required string Description { get; init; }

        /// <summary>
        /// Die Spielweisen - der Index ist <see cref="MinigameQuestion.Variant"/>, die erste ist die
        /// Vorgabe. Eine Art ohne Wahl hat genau eine.
        /// </summary>
        public required IReadOnlyList<MinigameVariant> Variants { get; init; }

        /// <summary>
        /// Die Namen der Spielweisen - <b>immer dieselbe Liste</b>. Eine neue je Abruf setzte die
        /// Auswahlliste der Maske zurueck, und deren Index -1 kaeme ueber die Bindung als Spielweise zurueck.
        /// </summary>
        public IReadOnlyList<string> VariantNames => variantNames ??= Variants.Select(v => v.Name).ToList();

        private IReadOnlyList<string>? variantNames;

        /// <summary>Ob die Art <see cref="MinigameQuestion.DurationSeconds"/> liest.</summary>
        public bool ShowDurationSeconds { get; init; }

        /// <summary>Ob die Art <see cref="MinigameQuestion.Rounds"/> liest.</summary>
        public bool ShowRounds { get; init; }

        /// <summary>Ob die Art <see cref="MinigameQuestion.Share"/> liest.</summary>
        public bool ShowShare { get; init; }

        public int DefaultDurationSeconds { get; init; } = 30;

        public int DefaultRounds { get; init; } = 1;

        public int DefaultShare { get; init; } = 100;

        // --- Was die Art mindestens braucht (V2): gelesen von MinigameChecks ---

        /// <summary>So viele Zeilen (Optionen, Begriffe, Aussagen) muss die Frage mindestens tragen.</summary>
        public required int MinOptions { get; init; }

        /// <summary>Wie die Zeilen in Meldungen heissen.</summary>
        public string OptionsName { get; init; } = "Antwortoptionen";

        /// <summary>So viele Mitspieler braucht das Spiel - geprueft erst beim Spielstart.</summary>
        public int MinPlayers { get; init; } = 1;

        /// <summary>Die Spielweise zum gespeicherten Index; ein unbekannter faellt auf die Vorgabe.</summary>
        public MinigameVariant VariantFor(int variant)
            => variant >= 0 && variant < Variants.Count ? Variants[variant] : Variants[0];

        /// <summary>Setzt Art und Parameter auf die Vorgaben dieser Art - beim Anlegen und beim Wechsel der Art.</summary>
        public void ApplyDefaults(MinigameQuestion frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            frage.Kind = Kind;
            frage.DurationSeconds = DefaultDurationSeconds;
            frage.Rounds = DefaultRounds;
            frage.Share = DefaultShare;
            frage.Variant = 0;
        }
    }
}
