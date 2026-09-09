using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.Globalization;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Punktekurve-Teil des Editors: nach welcher Kurve die Punkte je Hinweis sinken und womit
    /// dabei multipliziert wird.
    /// <para>
    /// <b>Nutzerentscheidung F16 vom 2026-09-09</b> - beide Kurven, je Frage wählbar, und der
    /// Faktor anpassbar. Sichtbar nur dort, wo die Punkte überhaupt je Hinweis sinken.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Ein Eintrag der Auswahlliste - der Wert und sein deutscher Name.</summary>
        /// <param name="Modus">Die Kurve.</param>
        /// <param name="Bezeichnung">Wie sie in der Liste heißt.</param>
        public record ScoreReductionModeInfo(ScoreReductionMode Modus, string Bezeichnung);

        /// <summary>Die beiden Kurven zur Wahl.</summary>
        public IReadOnlyList<ScoreReductionModeInfo> ScoreReductionModes { get; } =
        [
            new(ScoreReductionMode.Linear, "Gleichmäßig, letzter Hinweis halbiert"),
            new(ScoreReductionMode.Halving, "Jeder Hinweis halbiert"),
        ];

        /// <summary>
        /// Der Faktor als Text - damit ein halb getippter Wert die Frage nicht sofort verstellt.
        /// </summary>
        public string ScoreReductionFactor
        {
            get => (Question?.ScoreReductionFactor ?? Punkteabzug.Vorgabefaktor)
                .ToString("0.##", CultureInfo.CurrentCulture);
            set
            {
                if (Question == null)
                    return;

                // Unleserliches bleibt liegen, statt die Frage auf 0 zu setzen - und 0 oder 1
                // waeren keine Kurve mehr, sondern ein Sprung.
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var wert)
                    || wert <= 0 || wert >= 1)
                    return;

                Question.ScoreReductionFactor = wert;

                OnPropertyChanged();
                OnPropertyChanged(nameof(ScoreReductionHintText));
                Revalidate();
            }
        }

        /// <summary>
        /// Der Verlauf im Klartext, mit den Punkten <b>dieser</b> Frage und ihren Hinweisen.
        /// <para>
        /// <b>Gerechnet, nicht beschrieben</b> - aus <see cref="Punkteabzug"/>, derselben Stelle,
        /// aus der das Spiel liest. Eine erklärende Zeile, die die Zahlen selbst aufschreibt,
        /// läuft der Rechnung davon.
        /// </para>
        /// </summary>
        public string ScoreReductionHintText
        {
            get
            {
                var frage = Question;

                if (frage == null)
                    return string.Empty;

                var schritte = (frage.Steps ?? [])
                    .Count(s => !s.IsStart && !s.IsFinish);

                if (schritte <= 0 || frage.Points <= 0)
                    return "Die erreichbaren Punkte sinken mit jedem aufgedeckten Hinweis; "
                         + "erst der Auflösungsschritt gibt 0.";

                var verlauf = Enumerable.Range(1, schritte)
                    .Select(i => Punkteabzug.Verbleibend(
                        frage.Points, schritte, i, frage.ScoreReductionMode, frage.ScoreReductionFactor))
                    .ToArray();

                return $"Von {frage.Points} Punkten bleiben: "
                     + string.Join(", ", verlauf)
                     + " - und im Auflösungsschritt 0.";
            }
        }
    }
}
