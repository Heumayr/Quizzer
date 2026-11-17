using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Spielweise einer Art: der Name im Editor und der Regeltext fuer den Beamer.</summary>
    /// <param name="Name">Wie die Spielweise im Editor heisst.</param>
    /// <param name="RulesText">Was am Beamer ueber den Optionen steht.</param>
    public sealed record MinigameVariant(string Name, string RulesText);

    /// <summary>
    /// Wie eine Art im Teamspiel gewertet wird (M37 T8, Entwurf Abschnitt 7). Kein Wert 0 - ein Profil ohne Angabe ist
    /// kaputt, nicht „Standard"; ein unbekannter Wert wird wie <see cref="NichtImTeamspiel"/> behandelt (fail-closed).
    /// </summary>
    public enum MinispielTeamwertung
    {
        /// <summary>Die Einzelvorschläge eines Teams als Summe oder Durchschnitt (F-M37-11), auf die Mitglieder aufgeteilt.</summary>
        Zusammengefasst = 1,

        /// <summary>Je Person gewertet, jedes Ziel nur ein Gegner - Räuber, Duell, Teilen, Kartoffel, Glücksrad, Bluff.</summary>
        JePerson = 2,

        /// <summary>Wird im Teamspiel nicht gespielt - der Spielstart meldet es (<c>MinigameChecks.ForTeams</c>).</summary>
        NichtImTeamspiel = 3,

        /// <summary>Nur im Teamspiel, Team gegen Team (M37 T9, Tauziehen) - ohne Teamspiel meldet es der Spielstart.</summary>
        NurImTeamspiel = 4,
    }

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

        /// <summary>Welche Bedienung das Telefon bekommt - gelesen von <see cref="QuestionTypeProfile.LayoutFor"/>.</summary>
        public BuzzerControlsLayout Layout { get; init; } = BuzzerControlsLayout.KeySelect;

        /// <summary>Hoechstzahl der Runden, wo die Art welche spielt.</summary>
        public const int MaxRounds = 10;

        /// <summary>Ob die Art <see cref="MinigameQuestion.DurationSeconds"/> liest.</summary>
        public bool ShowDurationSeconds { get; init; }

        /// <summary>Ob die Art <see cref="MinigameQuestion.Rounds"/> liest.</summary>
        public bool ShowRounds { get; init; }

        /// <summary>Ob die Art <see cref="MinigameQuestion.Share"/> liest.</summary>
        public bool ShowShare { get; init; }

        public int DefaultDurationSeconds { get; init; } = 30;

        /// <summary>Wie die Maske <see cref="MinigameQuestion.DurationSeconds"/> nennt - die Stoppuhr liest sie als Zielzeit (M35 C1).</summary>
        public string DurationLabel { get; init; } = "Dauer (Sekunden)";

        /// <summary>Die Erklaerung dazu in der Maske.</summary>
        public string DurationToolTip { get; init; } = "Wie lange eine Runde läuft.";

        /// <summary>Kuerzeste und laengste erlaubte Dauer, wo die Art eine liest - geprueft von <see cref="MinigameChecks"/>.</summary>
        public int MinDurationSeconds { get; init; } = 1;

        public int MaxDurationSeconds { get; init; } = 600;

        public int DefaultRounds { get; init; } = 1;

        /// <summary>So viele Runden spielt eine Art ohne <see cref="ShowRounds"/> - der Bluff zwei Phasen (M35 C6).</summary>
        public int FixedRounds { get; init; } = 1;

        public int DefaultShare { get; init; } = 100;

        // --- Was die Art mindestens braucht (V2): gelesen von MinigameChecks ---

        /// <summary>So viele Zeilen (Optionen, Begriffe, Aussagen) muss die Frage mindestens tragen.</summary>
        public required int MinOptions { get; init; }

        /// <summary>Wie die Zeilen in Meldungen heissen.</summary>
        public string OptionsName { get; init; } = "Antwortoptionen";

        /// <summary>Ob je Zeile ein Haken „wahr" steht (Blitzrunde, M35 C5) - die Maske zeigt ihn nur dann.</summary>
        public bool ShowTruePerRow { get; init; }

        /// <summary>Ob eine Zeile mit diesem Text gespielt wird und darum als Zeile zaehlt - beim Buchstabensalat nur verwuerfelbare (M35 C5).</summary>
        public Func<string, bool> CountsAsOption { get; init; } = _ => true;

        /// <summary>Ob jede Runde eine eigene Zeile braucht (Erklaerbaer: je Runde ein Begriff, M35 C6) - dann mindestens so viele wie Runden.</summary>
        public bool OptionPerRound { get; init; }

        /// <summary>So viele Mitspieler braucht das Spiel - geprueft erst beim Spielstart.</summary>
        public int MinPlayers { get; init; } = 1;

        /// <summary>
        /// Wie der gewaehlte Mitspieler heisst („Gegner", „Partner"), wo der Ausloeser am Telefon einen
        /// waehlt (M35 C2); <c>null</c> bei Arten ohne Spielerwahl.
        /// </summary>
        public string? PartnerLabel { get; init; }

        /// <summary>Ob der Ausloeser einen Mitspieler waehlt - dann zeigt das Fragefenster die Paarwahl des Spielleiters.</summary>
        public bool ChoosesPartner => PartnerLabel != null;

        /// <summary>Wie der Waehlende in der Paarwahl des Spielleiters heisst - beim Gluecksrad dreht er (M35 C4).</summary>
        public string ChooserLabel { get; init; } = "Wer fordert";

        /// <summary>Ob der Spielleiter nur festlegt, wer an der Reihe ist, ohne Gegenueber - beim Erklaerbaer der Erklaerer (M35 C6).</summary>
        public bool ChooserOnly { get; init; }

        /// <summary>Ob das Fragefenster die Paarwahl des Spielleiters zeigt - mit Gegenueber oder nur den Waehlenden.</summary>
        public bool ShowsPairChoice => ChoosesPartner || ChooserOnly;

        /// <summary>Was der Rohwert am Ergebnis bei dieser Art bedeutet (M35 C9); <c>null</c>: kein Messwert, keine Bestenliste.</summary>
        public MinigameRecord? Record { get; init; }

        /// <summary>Wie die Art im Teamspiel gewertet wird (M37 T8) - Pflicht, damit eine neue Art es bewusst entscheidet.</summary>
        public required MinispielTeamwertung TeamScoring { get; init; }

        /// <summary>Was im Teamspiel hinter dem Regeltext steht, wo eine Art je Person mehr sagt als „nur gegen Gegner“ (M37 T8).</summary>
        public string? TeamRulesText { get; init; }

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

            // Das Layout haengt an der Art - dieselbe Rechnung wie Konstruktor und Reparatur.
            QuestionTypeProfiles.For(QuestionType.Minigame).ApplyTo(frage);
        }
    }
}
