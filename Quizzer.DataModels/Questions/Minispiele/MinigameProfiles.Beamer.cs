using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Beamer-Spiele (M35 C4): Zocker und Glücksrad.</summary>
    public static partial class MinigameProfiles
    {
        private static readonly MinigameProfile Crash = new()
        {
            Kind = MinigameKind.Crash,
            DisplayName = "Zocker",
            Description = "Am Beamer steigt ein Multiplikator ab ×1,00, bis er an einem geheimen Punkt abstürzt "
                        + "(spätestens bei ×10). Jeder tippt am Telefon „Aussteigen“, wann er will. Ohne Einsatz: "
                        + "wer vor dem Absturz aussteigt, bekommt die Feldpunkte mal seinem Multiplikator, wer noch "
                        + "drin ist, nichts. Mit Einsatz: jeder setzt den Anteil der Feldpunkte – Ausstieg bringt "
                        + "Einsatz mal (Multiplikator − 1), der Absturz kostet den Einsatz (gekappt, wenn das Spiel "
                        + "kein Minus erlaubt). Ob ein Ausstieg vor dem Absturz kam, entscheidet die Uhr des Servers.",

            // Der Index ist MinigameQuestion.Variant - ZockerEvaluator.MitEinsatzVariant zeigt auf den zweiten.
            Variants =
            [
                new("Ohne Einsatz",
                    "Der Multiplikator steigt – steigt rechtzeitig aus! Wer vor dem Absturz aussteigt, bekommt die "
                    + "Feldpunkte mal seinem Multiplikator. Wer noch drin ist, geht leer aus."),
                new("Mit Einsatz",
                    $"Jeder setzt {SharePlaceholder} % der Feldpunkte. Der Multiplikator steigt – wer vor dem Absturz "
                    + "aussteigt, gewinnt seinen Einsatz mal (Multiplikator − 1) dazu. Wer noch drin ist, verliert den Einsatz."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowShare = true,
            MinOptions = 0,
            OptionsName = "Zeilen",
        };

        private static readonly MinigameProfile WheelOfFortune = new()
        {
            Kind = MinigameKind.WheelOfFortune,
            DisplayName = "Glücksrad",
            Description = "Der Spielleiter dreht im Fragefenster, der Beamer zeigt das Rad. Fünf gleich große Felder für "
                        + "den, der das Feld aufgedeckt hat: Verdoppeln (der doppelte Anteil der Feldpunkte), Klauen "
                        + "(einem gewählten Mitspieler den Anteil abnehmen), Verschenken (einem gewählten Mitspieler den "
                        + "Anteil schenken), Pech (den Anteil verlieren) und Robin Hood (der Führende gibt dem Letzten den "
                        + "Anteil; bei Gleichstand teilen sich die Führenden bzw. die Letzten ihn). Das Ziel wählt der "
                        + "Auslöser am Telefon; fehlt er, wählt der Spielleiter. Ein Minus wird gekappt, wenn das Spiel "
                        + "kein Minus erlaubt.",
            Variants =
            [
                new("Glücksrad",
                    "Das Glücksrad! Wer das Feld aufgedeckt hat, dreht: Verdoppeln, Klauen, Verschenken, Pech oder "
                    + $"Robin Hood – es geht um {SharePlaceholder} % der Feldpunkte."),
            ],
            Layout = BuzzerControlsLayout.Minigame,
            ShowShare = true,
            MinOptions = 0,
            OptionsName = "Zeilen",

            // Klauen und Verschenken brauchen ein Gegenueber.
            MinPlayers = 2,
            PartnerLabel = "Ziel",
            ChooserLabel = "Wer dreht",
        };
    }
}
