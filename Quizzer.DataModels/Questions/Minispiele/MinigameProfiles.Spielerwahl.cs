using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Spiele mit Spielerwahl (M35 C2): Duell, Räuber und Wächter, Teilen oder Nehmen.</summary>
    public static partial class MinigameProfiles
    {
        // M35 C2: die drei Spiele mit Spielerwahl am Telefon. Der Anteil bewegt Punkte zwischen Spielern.
        private static readonly MinigameProfile Duel = new()
        {
            Kind = MinigameKind.Duel,
            TeamScoring = MinispielTeamwertung.JePerson,
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
            TeamScoring = MinispielTeamwertung.JePerson,
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
            TeamScoring = MinispielTeamwertung.JePerson,
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
            TeamRulesText = "Im Teamspiel ist der Partner aus einem anderen Team.",
        };
    }
}
