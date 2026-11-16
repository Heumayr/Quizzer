using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Die Minispiele der Typenschau (M35 B2) - eigene Teildatei, weil die Typenschau sonst ueber die
    /// Groessen-Obergrenze wuechse. Mehrheit und Reaktion im Raster; Minderheit (B3), Tippsprint und
    /// Stoppuhr (C1), Duell, Raeuber und Teilen (C2), Zocker (C4) im Vorrat - das Raster hat zwei Zeilen.
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Zeilen im Raster der Typenschau - mehr Fragen eines Typs liegen nur im Vorrat (M35 B3).</summary>
        private const int TypenschauZeilen = 2;

        private static IEnumerable<QuestionBase> TypenschauMinispiele(Guid kat)
        {
            yield return Mehrheit(kat, "Typenschau: Mehrheit", "MIN-1", 100, Difficulty.Level1,
                "Was isst die Runde lieber?", variante: 0,
                ["Pizza", "Pasta", "Sushi", "Schnitzel"]);

            yield return OhneZeilen(kat, "Typenschau: Reaktion", "MIN-3", 200, Difficulty.Level2,
                "Wer ist am schnellsten? Erst bei Grün tippen!", MinigameKind.Reaction);

            // Nur im Vorrat: das Raster hat zwei Zeilen (TypenschauZeilen), die Reaktion hat die zweite.
            yield return Mehrheit(kat, "Typenschau: Minderheit", "MIN-2", 200, Difficulty.Level2,
                "Wohin ginge die Runde am liebsten? (Hier gewinnt die kleinste Gruppe.)",
                variante: MehrheitEvaluator.MinorityVariant,
                ["Berge", "Meer", "Stadt"]);

            // M35 C1, ebenfalls nur im Vorrat.
            yield return OhneZeilen(kat, "Typenschau: Tippsprint", "MIN-4", 200, Difficulty.Level2,
                "Wer tippt am häufigsten? Zehn Sekunden, so schnell ihr könnt!", MinigameKind.TapSprint);

            yield return OhneZeilen(kat, "Typenschau: Stoppuhr", "MIN-5", 200, Difficulty.Level2,
                "Wer hat das beste Zeitgefühl? Stoppt nach genau zehn Sekunden!", MinigameKind.Stopwatch);

            // M35 C2, ebenfalls nur im Vorrat: die Spiele mit Spielerwahl.
            yield return OhneZeilen(kat, "Typenschau: Duell", "MIN-6", 200, Difficulty.Level2,
                "Wer das Feld aufgedeckt hat, fordert jemanden heraus: Schere, Stein, Papier!", MinigameKind.Duel);

            yield return OhneZeilen(kat, "Typenschau: Räuber und Wächter", "MIN-7", 200, Difficulty.Level2,
                "Bestehlen oder Schild? Alle wählen geheim und gleichzeitig.", MinigameKind.RobberAndGuard);

            yield return OhneZeilen(kat, "Typenschau: Teilen oder Nehmen", "MIN-8", 200, Difficulty.Level2,
                "Teilen oder nehmen? Zwei entscheiden geheim über die Punkte des Felds.", MinigameKind.ShareOrTake);

            // M35 C4, ebenfalls nur im Vorrat.
            yield return OhneZeilen(kat, "Typenschau: Zocker", "MIN-9", 200, Difficulty.Level2,
                "Wer traut sich am längsten? Steigt aus, bevor es abstürzt!", MinigameKind.Crash);
        }

        /// <summary>Arten im Telefonrahmen (Reaktion B3, Tippsprint und Stoppuhr C1): keine Zeilen, nur Art und Parameter.</summary>
        private static QuestionBase OhneZeilen(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe, string frageText,
            MinigameKind art)
        {
            var frage = new MinigameQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            MinigameProfiles.For(art).ApplyDefaults(frage);
            frage.MinusPoints = 0;
            frage.QuestionText = frageText;

            return frage;
        }

        /// <summary>Mehrheit: je Option ein Schritt, kein Haken - richtig ist, was die Gruppe waehlt.</summary>
        private static QuestionBase Mehrheit(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, int variante, string[] optionen)
        {
            var frage = new MinigameQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.MinusPoints = 0;
            frage.QuestionText = frageText;
            frage.ShowTextOnKeySelect = true;
            frage.Kind = MinigameKind.Majority;
            frage.Variant = variante;

            for (var i = 0; i < optionen.Length; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, optionen[i]));

            frage.BuzzerMaxAllowedKeySelect = 1;

            return frage;
        }
    }
}
