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

        // M35 C2: die drei Spiele mit Spielerwahl am Telefon. Der Anteil bewegt Punkte zwischen Spielern.
        private static readonly MinigameProfile Duel = new()
        {
            Kind = MinigameKind.Duel,
            DisplayName = "Duell",
            Description = "Wer das Feld aufgedeckt hat, wählt am Telefon einen Gegner; nur die beiden spielen, "
                        + "die anderen schauen zu. Der Sieger bekommt den Vorschlag „Anteil der Feldpunkte“, "
                        + "der Verlierer denselben Wert als Minus – gekappt, wenn das Spiel kein Minus erlaubt. "
                        + "Unentschieden: keine Bewegung. Fehlt der Auslöser, wählt der Spielleiter im Fragefenster.",

            // Der Index ist MinigameQuestion.Variant - DuellEvaluator.ReaktionVariant zeigt auf den zweiten.
            Variants =
            [
                new("Schere, Stein, Papier",
                    "Duell! Wer das Feld aufgedeckt hat, wählt einen Gegner. Schere, Stein, Papier – geheim und "
                    + $"gleichzeitig, Best of {RoundsPlaceholder}. Der Sieger nimmt dem Verlierer {SharePlaceholder} % der Feldpunkte ab."),
                new("Reaktion",
                    "Duell! Wer das Feld aufgedeckt hat, wählt einen Gegner. Wartet auf Grün und „JETZT!“ – die "
                    + $"beste Zeit gewinnt. Der Sieger nimmt dem Verlierer {SharePlaceholder} % der Feldpunkte ab."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowRounds = true,
            DefaultRounds = 3,
            ShowShare = true,
            DefaultShare = 50,
            MinOptions = 0,
            OptionsName = "Zeilen",
            MinPlayers = 2,
            PartnerLabel = "Gegner",
        };

        private static readonly MinigameProfile RobberAndGuard = new()
        {
            Kind = MinigameKind.RobberAndGuard,
            DisplayName = "Räuber und Wächter",
            Description = "Alle wählen am Telefon geheim und gleichzeitig: einen Mitspieler bestehlen oder das "
                        + "Schild heben. Raub bei jemandem ohne Schild: Dieb plus Anteil der Feldpunkte, Opfer "
                        + "dasselbe als Minus. Raub bei einem Schild: der Dieb zahlt den Anteil an das Opfer. "
                        + "Mehrere Räuber beim selben Opfer zählen je für sich; erlaubt das Spiel kein Minus, "
                        + "wird die Summe je Spieler einmal am Kontostand gekappt.",
            Variants =
            [
                new("Bestehlen oder Schild",
                    "Wählt geheim: einen Mitspieler bestehlen – oder das Schild heben. Wer jemanden ohne Schild "
                    + $"bestiehlt, nimmt ihm {SharePlaceholder} % der Feldpunkte ab; wer auf ein Schild trifft, zahlt sie ihm."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowShare = true,
            DefaultShare = 50,
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Zu zweit gaebe es nur einen moeglichen Raub - kein Spiel (V2).
            MinPlayers = 3,
        };

        private static readonly MinigameProfile ShareOrTake = new()
        {
            Kind = MinigameKind.ShareOrTake,
            DisplayName = "Teilen oder Nehmen",
            Description = "Wer das Feld aufgedeckt hat, wählt am Telefon einen Partner. Beide entscheiden geheim: "
                        + "teilen oder nehmen. Beide teilen: je die Hälfte der Feldpunkte; einer nimmt: er bekommt "
                        + "alles; beide nehmen: niemand bekommt etwas. Nur Plus-Vorschläge. Fehlt der Auslöser, "
                        + "wählt der Spielleiter im Fragefenster.",
            Variants =
            [
                new("Teilen oder Nehmen",
                    "Wer das Feld aufgedeckt hat, wählt einen Partner. Beide entscheiden geheim: teilen oder nehmen. "
                    + "Beide teilen – je die Hälfte. Einer nimmt – er bekommt alles. Beide nehmen – niemand bekommt etwas."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            MinOptions = 0,
            OptionsName = "Zeilen",
            MinPlayers = 2,
            PartnerLabel = "Partner",
        };

        /// <summary>Steht im Regeltext fuer <see cref="MinigameQuestion.DurationSeconds"/> - <see cref="RulesFor"/> setzt die Zahl ein.</summary>
        public const string DurationPlaceholder = "{Dauer}";

        /// <summary>Ebenso fuer <see cref="MinigameQuestion.Rounds"/> (M35 C2).</summary>
        public const string RoundsPlaceholder = "{Runden}";

        /// <summary>Ebenso fuer <see cref="MinigameQuestion.Share"/> (M35 C2).</summary>
        public const string SharePlaceholder = "{Anteil}";

        /// <summary>Alle Profile in der Reihenfolge, in der sie angeboten werden.</summary>
        public static IReadOnlyList<MinigameProfile> All { get; } =
            [Majority, Reaction, TapSprint, Stopwatch, Duel, RobberAndGuard, ShareOrTake];

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
