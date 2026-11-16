using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Die Minispiele der Typenschau (M35 B2) - eigene Teildatei, weil die Typenschau sonst ueber die
    /// Groessen-Obergrenze wuechse. Mehrheit und Reaktion im Raster, die Minderheit im Vorrat (B3).
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

            yield return Reaktion(kat, "Typenschau: Reaktion", "MIN-3", 200, Difficulty.Level2,
                "Wer ist am schnellsten? Erst bei Grün tippen!");

            // Nur im Vorrat: das Raster hat zwei Zeilen (TypenschauZeilen), die Reaktion hat die zweite.
            yield return Mehrheit(kat, "Typenschau: Minderheit", "MIN-2", 200, Difficulty.Level2,
                "Wohin ginge die Runde am liebsten? (Hier gewinnt die kleinste Gruppe.)",
                variante: MehrheitEvaluator.MinorityVariant,
                ["Berge", "Meer", "Stadt"]);
        }

        /// <summary>Reaktion (M35 B3): keine Zeilen, nur Art und Runden - der Rest passiert am Telefon.</summary>
        private static QuestionBase Reaktion(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe, string frageText)
        {
            var frage = new MinigameQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(frage);
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
